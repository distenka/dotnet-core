using Distenka.Client.Runs;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-030: <c>POST /api/runs</c> through <see cref="IRunsClient.SubmitAsync(string, JsonElement, SubmitRunOptions, System.Threading.CancellationToken)"/>.
	/// Statuses, headers and bodies from docs/api-run-submission.md §The request, §The responses.
	/// </summary>
	public class Run_submission
	{
		const string Accepted = @"{""runId"":""r-1"",""processorId"":""etl-loyalty"",""trackingUrl"":""https://api.example/api/runs/r-1/details""}";

		static readonly JsonElement Configuration = JsonDocument.Parse(@"{""path"":""/in/a.csv"",""mode"":""full""}").RootElement.Clone();

		static (IDistenkaClient client, ScriptedHttpHandler handler) Create(ScriptedHttpHandler handler, int maxRetries = 3)
		{
			var options = new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example/"),
				Credential = new Authentication.AccountKeyCredential("DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"),
				MaxRetries = maxRetries,
				MaxRetryDelay = TimeSpan.FromSeconds(1),
			};
			return (new DistenkaClient(new HttpClient(handler), options), handler);
		}

		[Fact]
		public async Task a_submission_posts_the_documented_body_to_api_runs()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Runs.SubmitAsync("etl-loyalty", Configuration);

			var request = handler.Requests.Single();
			request.Method.Should().Be(HttpMethod.Post);
			request.RequestUri.Should().Be(new Uri("https://api.example/api/runs"));
			request.Content.Headers.ContentType.MediaType.Should().Be("application/json");

			using var body = JsonDocument.Parse(handler.Bodies.Single());
			body.RootElement.GetProperty("processorId").GetString().Should().Be("etl-loyalty");
			body.RootElement.GetProperty("configurationJson").GetRawText().Should().Be(Configuration.GetRawText(), "the configuration runs exactly as sent");
			body.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("processorId", "configurationJson");
		}

		[Fact]
		public async Task a_202_is_a_new_run()
		{
			// AC 1, first half.
			var (client, _) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			var accepted = await client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "order-42" });

			accepted.RunId.Should().Be("r-1");
			accepted.ProcessorId.Should().Be("etl-loyalty");
			accepted.TrackingUrl.Should().Be("https://api.example/api/runs/r-1/details");
			accepted.Replayed.Should().BeFalse();
		}

		[Fact]
		public async Task a_200_with_Idempotency_Replayed_is_the_first_run_replayed()
		{
			// AC 1, second half: the same key submitted twice; the replay carries the first run id.
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(202, Accepted),
				Reply(200, Accepted, ("Idempotency-Replayed", "true"))));
			var options = new SubmitRunOptions { IdempotencyKey = "order-42" };

			var first = await client.Runs.SubmitAsync("etl-loyalty", Configuration, options);
			var second = await client.Runs.SubmitAsync("etl-loyalty", Configuration, options);

			second.Replayed.Should().BeTrue();
			second.RunId.Should().Be(first.RunId);
			handler.Requests.Should().OnlyContain(r => r.Headers.GetValues("Idempotency-Key").Single() == "order-42");
		}

		[Fact]
		public async Task without_a_key_no_Idempotency_Key_header_is_sent()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Runs.SubmitAsync("etl-loyalty", Configuration);

			handler.Requests.Single().Headers.Contains("Idempotency-Key").Should().BeFalse();
		}

		[Fact]
		public async Task idempotency_key_reuse_is_terminal()
		{
			// AC 2.
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(409, @"{""code"":""idempotency_key_reuse"",""error"":""This key belongs to a different request.""}")));

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "order-42" });

			var thrown = (await submit.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.IdempotencyKeyReuse);
			thrown.IsRetryable.Should().BeFalse();
			handler.Requests.Should().HaveCount(1);
		}

		[Fact]
		public async Task idempotency_key_in_flight_is_retryable()
		{
			// AC 2: surfaced with IsRetryable=true when the retries are spent (here: none allowed)…
			var (client, _) = Create(new ScriptedHttpHandler(
				Reply(409, @"{""code"":""idempotency_key_in_flight"",""error"":""Still starting.""}")), maxRetries: 0);

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "order-42" });

			var thrown = (await submit.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.IdempotencyKeyInFlight);
			thrown.IsRetryable.Should().BeTrue();
		}

		[Fact]
		public async Task idempotency_key_in_flight_is_retried_into_the_replay()
		{
			// …and with retries allowed, the client retries with the same key and gets the 200 replay (docs: "Retry; the
			// retry will get the 200 replay").
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(409, @"{""code"":""idempotency_key_in_flight"",""error"":""Still starting.""}"),
				Reply(200, Accepted, ("Idempotency-Replayed", "true"))), maxRetries: 1);

			var accepted = await client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "order-42" });

			accepted.Replayed.Should().BeTrue();
			handler.Requests.Should().HaveCount(2);
			handler.Bodies.Distinct().Should().ContainSingle("each attempt sends the same body");
		}

		[Fact]
		public async Task a_submission_without_a_key_is_never_retried()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(429, @"{""code"":""queue_depth_exceeded"",""error"":""Queue full."",""retryAfterSeconds"":1,""queued"":50,""limit"":50}", ("Retry-After", "1"))));

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration);

			var thrown = (await submit.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.Code.Should().Be(DistenkaErrorCodes.QueueDepthExceeded);
			thrown.RetryAfter.Should().Be(TimeSpan.FromSeconds(1));
			thrown.Body.Value.GetProperty("limit").GetInt32().Should().Be(50);
			handler.Requests.Should().HaveCount(1, "a POST without Idempotency-Key could start a second run (AD-6)");
		}

		[Fact]
		public async Task a_submission_with_a_key_is_retried_after_backpressure()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(
				Reply(429, @"{""code"":""platform_backpressure"",""error"":""Not now."",""retryAfterSeconds"":1}", ("Retry-After", "1")),
				Reply(202, Accepted)));

			var accepted = await client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "order-43" });

			accepted.Replayed.Should().BeFalse("the refused attempt started nothing and freed the key");
			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task an_unknown_processor_is_a_404()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(404)));

			var submit = () => client.Runs.SubmitAsync("missing", Configuration);

			(await submit.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task a_malformed_key_is_reported_by_the_platform()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(400, @"{""code"":""idempotency_key_malformed"",""error"":""Bad key.""}")));

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = "has space" });

			(await submit.Should().ThrowAsync<DistenkaApiException>()).Which.Code.Should().Be(DistenkaErrorCodes.IdempotencyKeyMalformed);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("  ")]
		public async Task a_processor_id_is_required(string processorId)
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var submit = () => client.Runs.SubmitAsync(processorId, Configuration);

			await submit.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Theory]
		[InlineData("[1,2]")]
		[InlineData("\"text\"")]
		[InlineData("null")]
		public async Task the_configuration_must_be_a_JSON_object(string json)
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", JsonDocument.Parse(json).RootElement.Clone());

			await submit.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public async Task an_empty_idempotency_key_is_refused()
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration, new SubmitRunOptions { IdempotencyKey = " " });

			await submit.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public async Task a_2xx_without_a_run_id_is_an_error()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(202, "{}")));

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", Configuration);

			await submit.Should().ThrowAsync<DistenkaApiException>();
		}

		[Fact]
		public async Task a_typed_configuration_is_serialized_with_the_callers_context()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(202, Accepted)));

			await client.Runs.SubmitAsync("etl-loyalty", new EtlConfig { Path = "/in/a.csv", Mode = "full" }, TestConfigContext.Default.EtlConfig);

			using var body = JsonDocument.Parse(handler.Bodies.Single());
			body.RootElement.GetProperty("configurationJson").GetRawText().Should().Be(@"{""path"":""/in/a.csv"",""mode"":""full""}");
		}

		[Fact]
		public async Task the_typed_overload_requires_type_information()
		{
			var (client, _) = Create(new ScriptedHttpHandler());

			var submit = () => client.Runs.SubmitAsync("etl-loyalty", new EtlConfig(), (System.Text.Json.Serialization.Metadata.JsonTypeInfo<EtlConfig>)null);

			await submit.Should().ThrowAsync<ArgumentNullException>();
		}
	}

	public sealed class EtlConfig
	{
		public string Path { get; set; }
		public string Mode { get; set; }
	}

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(EtlConfig))]
	internal sealed partial class TestConfigContext : JsonSerializerContext
	{
	}
}
