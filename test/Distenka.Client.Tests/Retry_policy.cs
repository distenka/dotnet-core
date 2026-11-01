using Distenka.Client.Authentication;
using Distenka.Client.Internal;
using Distenka.Client.Resilience;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-025: retries honour <c>Retry-After</c>, never replay a <c>POST</c> without an <c>Idempotency-Key</c> (AD-6) and
	/// never repeat a terminal failure. Statuses and codes from docs/api-run-submission.md and docs/api-invocation.md.
	/// </summary>
	public class Retry_policy
	{
		const string Token = "DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

		/// <summary>Answers each request with the next scripted response and records every request.</summary>
		sealed class ScriptedHandler : HttpMessageHandler
		{
			readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> script;

			public ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) =>
				script = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(steps);

			public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Requests.Add(request);
				if (script.Count == 0)
					throw new InvalidOperationException("Unexpected extra request.");
				var response = script.Dequeue()(request);
				response.RequestMessage = request;
				return Task.FromResult(response);
			}
		}

		static Func<HttpRequestMessage, HttpResponseMessage> Status(int status, string code = null, int? retryAfterSeconds = null, bool replayed = false) => _ =>
		{
			var response = new HttpResponseMessage((HttpStatusCode)status);
			if (code != null)
				response.Content = new StringContent($"{{\"code\":\"{code}\",\"error\":\"scripted\"}}", Encoding.UTF8, "application/json");
			if (retryAfterSeconds is int seconds)
				response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
			if (replayed)
				response.Headers.TryAddWithoutValidation("Idempotency-Replayed", "true");
			return response;
		};

		static Func<HttpRequestMessage, HttpResponseMessage> NetworkFailure() => _ => throw new HttpRequestException("connection reset");

		sealed class Recorder
		{
			public List<TimeSpan> Delays { get; } = new List<TimeSpan>();
			public Task Delay(TimeSpan wait, CancellationToken token)
			{
				Delays.Add(wait);
				return Task.CompletedTask;
			}
		}

		static (HttpTransport transport, ScriptedHandler handler, Recorder delays) Create(ScriptedHandler handler, int maxRetries = 3, TimeSpan? maxDelay = null, IDistenkaCredential credential = null)
		{
			var delays = new Recorder();
			var options = new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/"), Credential = credential ?? new AccountKeyCredential(Token) };
			var policy = new RetryPolicy(maxRetries, maxDelay ?? TimeSpan.FromSeconds(60), delays.Delay, () => 0.5);
			return (new HttpTransport(new HttpClient(handler), options, policy), handler, delays);
		}

		static Func<HttpRequestMessage> Get(HttpTransport t) => () => t.CreateRequest(HttpMethod.Get, "api/runs/abc/details");

		static Func<HttpRequestMessage> Post(HttpTransport t, string idempotencyKey = null) => () =>
		{
			var request = t.CreateRequest(HttpMethod.Post, "api/runs");
			request.Content = new StringContent("{\"processorId\":\"etl\",\"configurationJson\":{}}", Encoding.UTF8, "application/json");
			if (idempotencyKey != null)
				request.Headers.Add("Idempotency-Key", idempotencyKey);
			return request;
		};

		static Task<HttpResponseMessage> Send(HttpTransport t, Func<HttpRequestMessage> create, bool replaySafe = false, CancellationToken token = default) =>
			t.SendWithRetryAsync(create, HttpCompletionOption.ResponseHeadersRead, token, replaySafe);

		// ---- AC 1 --------------------------------------------------------------------------------------------------------

		[Fact]
		public async Task a_GET_answered_429_with_Retry_After_2_is_retried_once_after_2_seconds()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(429, "rate_limit_exceeded", 2), Status(200)));

			using var response = await Send(t, Get(t));

			response.StatusCode.Should().Be(HttpStatusCode.OK);
			handler.Requests.Should().HaveCount(2);
			delays.Delays.Should().Equal(TimeSpan.FromSeconds(2));
		}

		[Fact]
		public async Task the_Retry_After_wait_really_happens()
		{
			// AC 1 "after ≥ 2 s", with the real clock.
			var handler = new ScriptedHandler(Status(429, "rate_limit_exceeded", 2), Status(200));
			var transport = new HttpTransport(new HttpClient(handler), new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/") });
			var clock = Stopwatch.StartNew();

			using var response = await Send(transport, Get(transport));

			clock.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9));
			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task a_POST_without_an_Idempotency_Key_is_never_retried()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(429, "queue_depth_exceeded", 2)));

			var send = () => Send(t, Post(t));

			var thrown = (await send.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.QueueDepthExceeded);
			thrown.RetryAfter.Should().Be(TimeSpan.FromSeconds(2), "the caller can schedule the retry itself");
			thrown.IsRetryable.Should().BeTrue();
			handler.Requests.Should().HaveCount(1);
			delays.Delays.Should().BeEmpty();
		}

		[Fact]
		public async Task a_POST_without_a_key_is_not_retried_after_a_network_error_either()
		{
			var (t, handler, _) = Create(new ScriptedHandler(NetworkFailure()));

			var send = () => Send(t, Post(t));

			await send.Should().ThrowAsync<HttpRequestException>();
			handler.Requests.Should().HaveCount(1);
		}

		// ---- AC 2 --------------------------------------------------------------------------------------------------------

		[Fact]
		public async Task a_POST_with_an_Idempotency_Key_is_retried_on_503_and_the_replay_is_recognised()
		{
			var (t, handler, _) = Create(new ScriptedHandler(Status(503, "capability_unavailable", 1), Status(200, replayed: true)));

			using var response = await Send(t, Post(t, "order-42"));

			response.StatusCode.Should().Be(HttpStatusCode.OK);
			HttpTransport.IsReplayed(response).Should().BeTrue("RunAccepted.Replayed (DCS-030) is read from this");
			handler.Requests.Should().HaveCount(2);
			handler.Requests.Should().OnlyContain(r => r.Headers.GetValues("Idempotency-Key").Single() == "order-42");
		}

		[Fact]
		public async Task every_attempt_is_a_fresh_request_with_its_own_body_and_authorization()
		{
			var (t, handler, _) = Create(new ScriptedHandler(Status(503), Status(502), Status(202)));

			using var response = await Send(t, Post(t, "k-1"));

			handler.Requests.Should().HaveCount(3).And.OnlyHaveUniqueItems();
			handler.Requests.Should().OnlyContain(r => r.Headers.Authorization.Parameter == Token && r.Content != null);
		}

		[Fact]
		public async Task a_202_that_is_not_a_replay_is_not_reported_as_one()
		{
			var (t, _, _) = Create(new ScriptedHandler(Status(202)));

			using var response = await Send(t, Post(t, "k-2"));

			HttpTransport.IsReplayed(response).Should().BeFalse();
		}

		// ---- AC 3 and the other terminal outcomes ------------------------------------------------------------------------

		[Theory]
		[InlineData(402, "quota_exhausted")]
		[InlineData(409, "idempotency_key_reuse")]
		[InlineData(409, "processor_unavailable")]
		[InlineData(409, "run_already_ended")]
		[InlineData(400, "idempotency_key_malformed")]
		[InlineData(403, "scope_violation")]
		[InlineData(401, null)]
		[InlineData(404, null)]
		[InlineData(413, null)]
		[InlineData(500, null)]
		public async Task terminal_failures_are_never_retried_even_when_the_request_is_replay_safe(int status, string code)
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(status, code)));

			var send = () => Send(t, Post(t, "k-3"));

			(await send.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be((HttpStatusCode)status);
			handler.Requests.Should().HaveCount(1);
			delays.Delays.Should().BeEmpty();
		}

		[Fact]
		public async Task idempotency_key_in_flight_is_retried_with_the_same_key()
		{
			var (t, handler, _) = Create(new ScriptedHandler(Status(409, "idempotency_key_in_flight"), Status(200, replayed: true)));

			using var response = await Send(t, Post(t, "k-4"));

			HttpTransport.IsReplayed(response).Should().BeTrue();
			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task a_401_is_surfaced_at_once_and_the_credential_asked_once()
		{
			var credential = new CountingCredential(new AccountKeyCredential(Token));
			var (t, handler, _) = Create(new ScriptedHandler(Status(401)), credential: credential);

			var send = () => Send(t, Get(t));

			(await send.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
			handler.Requests.Should().HaveCount(1);
			credential.Calls.Should().Be(1);
		}

		// ---- Limits ------------------------------------------------------------------------------------------------------

		[Fact]
		public async Task retries_stop_after_MaxRetries_and_the_last_error_is_thrown()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(429, "platform_overloaded", 1), Status(429, "platform_overloaded", 1),
				Status(429, "platform_overloaded", 1), Status(429, "platform_backpressure", 5)));

			var send = () => Send(t, Get(t));

			var thrown = (await send.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.PlatformBackpressure);
			thrown.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
			handler.Requests.Should().HaveCount(4, "1 attempt + MaxRetries (3)");
			delays.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
		}

		[Fact]
		public async Task MaxRetries_0_disables_retries()
		{
			var (t, handler, _) = Create(new ScriptedHandler(Status(503)), maxRetries: 0);

			var send = () => Send(t, Get(t));

			await send.Should().ThrowAsync<DistenkaApiException>();
			handler.Requests.Should().HaveCount(1);
		}

		[Fact]
		public async Task a_Retry_After_beyond_MaxRetryDelay_ends_the_retries()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(429, "rate_limit_exceeded", 120)), maxDelay: TimeSpan.FromSeconds(60));

			var send = () => Send(t, Get(t));

			(await send.Should().ThrowAsync<DistenkaApiException>()).Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(120));
			handler.Requests.Should().HaveCount(1);
			delays.Delays.Should().BeEmpty();
		}

		[Fact]
		public async Task without_Retry_After_the_wait_is_jittered_from_200_ms_and_capped()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(Status(502), Status(504), Status(503), Status(200)), maxDelay: TimeSpan.FromMilliseconds(500));

			using var response = await Send(t, Get(t));

			handler.Requests.Should().HaveCount(4);
			// random = 0.5: 200 + 0.5 × (600 − 200) = 400 ms; then 200 + 0.5 × (1200 − 200) = 700 → capped 500; again 500.
			delays.Delays.Should().Equal(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
		}

		[Fact]
		public async Task a_network_error_on_a_GET_is_retried()
		{
			var (t, handler, delays) = Create(new ScriptedHandler(NetworkFailure(), Status(200)));

			using var response = await Send(t, Get(t));

			response.StatusCode.Should().Be(HttpStatusCode.OK);
			handler.Requests.Should().HaveCount(2);
			delays.Delays.Should().ContainSingle();
		}

		[Fact]
		public async Task an_operation_documented_as_idempotent_can_opt_in()
		{
			// e.g. POST /api/runs/{id}/cancel: "Repeating a cancel is safe" (docs/api-run-cancellation.md).
			var (t, handler, _) = Create(new ScriptedHandler(Status(503), Status(200)));

			using var response = await Send(t, () => t.CreateRequest(HttpMethod.Post, "api/runs/abc/cancel"), replaySafe: true);

			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task cancellation_during_the_wait_stops_the_retries()
		{
			var handler = new ScriptedHandler(Status(429, "rate_limit_exceeded", 30));
			var transport = new HttpTransport(new HttpClient(handler), new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/") });
			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

			var send = () => Send(transport, Get(transport), token: cts.Token);

			await send.Should().ThrowAsync<OperationCanceledException>();
			handler.Requests.Should().HaveCount(1);
		}

		[Fact]
		public void replay_safety_follows_the_method_and_the_Idempotency_Key()
		{
			RetryPolicy.IsReplaySafe(new HttpRequestMessage(HttpMethod.Get, "https://x/")).Should().BeTrue();
			RetryPolicy.IsReplaySafe(new HttpRequestMessage(HttpMethod.Head, "https://x/")).Should().BeTrue();
			RetryPolicy.IsReplaySafe(new HttpRequestMessage(HttpMethod.Post, "https://x/")).Should().BeFalse();
			RetryPolicy.IsReplaySafe(new HttpRequestMessage(HttpMethod.Delete, "https://x/")).Should().BeFalse();

			var keyed = new HttpRequestMessage(HttpMethod.Post, "https://x/");
			keyed.Headers.Add("Idempotency-Key", "k");
			RetryPolicy.IsReplaySafe(keyed).Should().BeTrue();
		}

		[Fact]
		public void the_default_policy_comes_from_the_options()
		{
			var transport = new HttpTransport(new HttpClient(), new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/"), MaxRetries = 5, MaxRetryDelay = TimeSpan.FromSeconds(10) });

			transport.RetryPolicy.MaxRetries.Should().Be(5);
			transport.RetryPolicy.MaxDelay.Should().Be(TimeSpan.FromSeconds(10));
			new DistenkaClientOptions().MaxRetries.Should().Be(3);
			new DistenkaClientOptions().MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(60));
		}

		[Theory]
		[InlineData(-1, 60, "MaxRetries")]
		[InlineData(11, 60, "MaxRetries")]
		[InlineData(3, 0, "MaxRetryDelay")]
		[InlineData(3, 601, "MaxRetryDelay")]
		public void retry_options_are_validated(int maxRetries, int maxDelaySeconds, string expected)
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/"), MaxRetries = maxRetries, MaxRetryDelay = TimeSpan.FromSeconds(maxDelaySeconds) };

			new DistenkaClientOptionsValidator().Validate(null, options).Failures.Should().ContainSingle(f => f.Contains(expected));
		}

		sealed class CountingCredential : IDistenkaCredential
		{
			readonly IDistenkaCredential inner;
			public CountingCredential(IDistenkaCredential inner) => this.inner = inner;
			public int Calls { get; private set; }

			public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken)
			{
				Calls++;
				return inner.GetBearerTokenAsync(cancellationToken);
			}
		}
	}
}
