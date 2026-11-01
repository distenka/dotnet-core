using Distenka.Client.Authentication;
using Distenka.Client.Invocations;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-060: <c>POST /v1/invoke/{pluginId}</c> with an invocation key (docs/api-invocation.md). The <c>202</c> body is the
	/// doc's example verbatim; statuses and codes from its response table and §Limits.
	/// </summary>
	public class Plugin_invocation
	{
		const string InvokeKey = "DKI-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

		const string Accepted = @"{
  ""runId"": ""…"",
  ""status"": ""Pending"",
  ""resultCapability"": ""drc1.…"",
  ""resultCapabilityExpiresAt"": ""2026-09-19T10:02:11+00:00""
}";

		static readonly DateTimeOffset ExpiresAt = new DateTimeOffset(2026, 9, 19, 10, 2, 11, TimeSpan.Zero);

		static readonly JsonElement Input = JsonDocument.Parse(@"{""message"":""hello"",""sleepSeconds"":5}").RootElement.Clone();

		static (IDistenkaClient client, ScriptedHttpHandler handler) Create(ScriptedHttpHandler handler, int maxRetries = 3) =>
			(new DistenkaClient(new HttpClient(handler), new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example/"),
				Credential = new InvocationKeyCredential(InvokeKey),
				MaxRetries = maxRetries,
				MaxRetryDelay = TimeSpan.FromSeconds(1),
			}), handler);

		[Fact]
		public async Task an_invocation_posts_the_input_to_v1_invoke_with_the_invocation_key()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Invocations.InvokeAsync("echo", Input);

			var request = handler.Requests.Single();
			request.Method.Should().Be(HttpMethod.Post);
			request.RequestUri.Should().Be(new Uri("https://api.example/v1/invoke/echo"));
			request.Headers.Authorization.Parameter.Should().Be(InvokeKey);
			handler.Bodies.Single().Should().Be(Input.GetRawText(), "the input is the body, exactly as given");
			request.Headers.Contains("Callback-Url").Should().BeFalse();
			request.Headers.Contains("Idempotency-Key").Should().BeFalse();
		}

		[Fact]
		public async Task a_202_returns_a_usable_capability_with_its_expiry()
		{
			// AC 1.
			var (client, _) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			var accepted = await client.Invocations.InvokeAsync("echo", Input);

			accepted.RunId.Should().Be("…");
			accepted.Status.Should().Be("Pending");
			accepted.Replayed.Should().BeFalse();
			accepted.ResultCapabilityExpiresAt.Should().Be(ExpiresAt);
			accepted.ResultCapability.Should().NotBeNull();
			accepted.ResultCapability.ExpiresAt.Should().Be(ExpiresAt, "the expiry is attached to the credential");
			(await accepted.ResultCapability.GetBearerTokenAsync(CancellationToken.None)).Should().Be("drc1.…");
		}

		[Fact]
		public async Task a_null_capability_is_null()
		{
			// AC 1: the deployment has no capability signing key — the run is accepted, its result cannot be read via the API.
			var (client, _) = Create(new ScriptedHttpHandler(Reply(202,
				@"{""runId"":""r-1"",""status"":""Pending"",""resultCapability"":null,""resultCapabilityExpiresAt"":null}")));

			var accepted = await client.Invocations.InvokeAsync("echo", Input);

			accepted.RunId.Should().Be("r-1");
			accepted.ResultCapability.Should().BeNull();
		}

		[Fact]
		public async Task idempotency_key_and_callback_url_are_sent_as_headers()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Invocations.InvokeAsync("echo", Input,
				new InvokeOptions { IdempotencyKey = "order-42", CallbackUrl = new Uri("https://hooks.example.com/distenka?x=1") });

			var request = handler.Requests.Single();
			request.Headers.GetValues("Idempotency-Key").Should().Equal("order-42");
			request.Headers.GetValues("Callback-Url").Should().Equal("https://hooks.example.com/distenka?x=1");
		}

		[Fact]
		public async Task a_200_replay_is_recognised()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(200, Accepted, ("Idempotency-Replayed", "true"))));

			var accepted = await client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { IdempotencyKey = "order-42" });

			accepted.Replayed.Should().BeTrue();
		}

		[Fact]
		public async Task quota_exhausted_is_terminal_and_carries_the_limit()
		{
			// AC 2, first half: "Waiting does not help … The body carries the limit."
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(402, @"{""code"":""quota_exhausted"",""error"":""This key has spent its allowance."",""limit"":1000}")));

			var invoke = () => client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { IdempotencyKey = "order-42" });

			var thrown = (await invoke.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.QuotaExhausted);
			thrown.IsRetryable.Should().BeFalse();
			thrown.Limit.Should().Be(1000);
			handler.Requests.Should().HaveCount(1, "a 402 is never retried, even with a key");
		}

		[Theory]
		[InlineData("rate_limit_exceeded")]
		[InlineData("queue_depth_exceeded")]
		[InlineData("platform_backpressure")]
		[InlineData("platform_overloaded")]
		public async Task a_429_carries_Retry_After(string code)
		{
			// AC 2, second half (no key → not retried, so the 429 surfaces at once).
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(429, $@"{{""code"":""{code}"",""error"":""Not now.""}}", ("Retry-After", "7"))));

			var invoke = () => client.Invocations.InvokeAsync("echo", Input);

			var thrown = (await invoke.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(code);
			thrown.RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
			thrown.IsRetryable.Should().BeTrue();
			handler.Requests.Should().HaveCount(1, "an invocation without a key is never repeated — each one is billed");
		}

		[Fact]
		public async Task a_keyed_invocation_is_retried_after_a_429_with_the_same_key()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(429, @"{""code"":""platform_overloaded"",""error"":""Not now.""}", ("Retry-After", "1")),
				Reply(202, Accepted)));

			var accepted = await client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { IdempotencyKey = "order-44" });

			accepted.RunId.Should().Be("…");
			handler.Requests.Should().HaveCount(2);
			handler.Requests.Should().OnlyContain(r => r.Headers.GetValues("Idempotency-Key").Single() == "order-44");
			handler.Bodies.Distinct().Should().ContainSingle();
		}

		[Theory]
		[InlineData(403, "scope_violation")]
		[InlineData(403, "invocation_key_required")]
		[InlineData(409, "processor_unavailable")]
		[InlineData(400, null)]
		[InlineData(401, null)]
		[InlineData(404, null)]
		[InlineData(413, null)]
		public async Task refusals_surface_as_exceptions(int status, string code)
		{
			var body = code == null ? @"{""error"":""refused""}" : $@"{{""code"":""{code}"",""error"":""refused""}}";
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(status, body)));

			var invoke = () => client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { IdempotencyKey = "k" });

			var thrown = (await invoke.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.StatusCode.Should().Be((HttpStatusCode)status);
			thrown.Code.Should().Be(code);
			handler.Requests.Should().HaveCount(1);
		}

		[Theory]
		[InlineData(400, DistenkaErrorCodes.CallbackNotHttps)]
		[InlineData(400, DistenkaErrorCodes.CallbackUserInfo)]
		[InlineData(400, DistenkaErrorCodes.CallbackTooLong)]
		[InlineData(400, DistenkaErrorCodes.CallbackMalformed)]
		[InlineData(400, DistenkaErrorCodes.CallbackUnresolvable)]
		[InlineData(400, DistenkaErrorCodes.CallbackPrivateAddress)]
		[InlineData(409, DistenkaErrorCodes.CallbacksUnavailable)]
		public async Task a_refused_callback_is_terminal(int status, string code)
		{
			// AC 4 (re-homed from DCS-022): docs/api-callbacks.md §What is refused — a refused callback is a 400 (409 when the
			// deployment cannot sign callbacks), never a quiet 202, and the answer will not change on a retry.
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(status, $@"{{""code"":""{code}"",""error"":""Refused.""}}")));

			var invoke = () => client.Invocations.InvokeAsync("echo", Input,
				new InvokeOptions { IdempotencyKey = "k", CallbackUrl = new Uri("https://hooks.example.com/distenka") });

			var thrown = (await invoke.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.StatusCode.Should().Be((HttpStatusCode)status);
			thrown.Code.Should().Be(code);
			thrown.IsRetryable.Should().BeFalse();
			handler.Requests.Should().HaveCount(1, "a refused callback is not retried, even with an idempotency key");
		}

		[Fact]
		public async Task a_typed_input_is_serialized_with_the_callers_context()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Invocations.InvokeAsync("echo", new EchoInput { Message = "hi", SleepSeconds = 1 }, TestInputContext.Default.EchoInput);

			handler.Bodies.Single().Should().Be(@"{""message"":""hi"",""sleepSeconds"":1}");
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" ")]
		public async Task a_plugin_id_is_required(string pluginId)
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var invoke = () => client.Invocations.InvokeAsync(pluginId, Input);

			await invoke.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public async Task the_input_must_be_a_JSON_object()
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var invoke = () => client.Invocations.InvokeAsync("echo", JsonDocument.Parse("[1]").RootElement.Clone());

			await invoke.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public async Task a_relative_callback_url_is_refused()
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var invoke = () => client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { CallbackUrl = new Uri("/hook", UriKind.Relative) });

			await invoke.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public async Task an_empty_idempotency_key_is_refused()
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var invoke = () => client.Invocations.InvokeAsync("echo", Input, new InvokeOptions { IdempotencyKey = "" });

			await invoke.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public void Limit_is_null_without_a_numeric_limit()
		{
			new DistenkaApiException("m", HttpStatusCode.PaymentRequired).Limit.Should().BeNull();
			new DistenkaApiException("m", HttpStatusCode.PaymentRequired, body: JsonDocument.Parse(@"{""limit"":""many""}").RootElement.Clone()).Limit.Should().BeNull();
		}
	}

	public sealed class EchoInput
	{
		public string Message { get; set; }
		public int SleepSeconds { get; set; }
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(EchoInput))]
	internal sealed partial class TestInputContext : JsonSerializerContext
	{
	}
}
