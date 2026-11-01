using Distenka.Client.Authentication;
using Distenka.Client.Internal;
using Distenka.Client.Invocations;
using Distenka.Client.Resilience;
using Distenka.Client.Runs;
using Distenka.Client.Serialization;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-062: reading an invoked run's result with its capability — <c>GET /api/runs/result</c> (docs/api-run-results.md).
	/// The <c>200</c> body is the doc's example verbatim; statuses and codes from its table; polling rules from ADR-007.
	/// </summary>
	public class Run_result
	{
		const string InvokeKey = "DKI-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
		const string Capability = "drc1.eyJydW4iOiJyLTEifQ.c2ln";

		const string DocExample = @"{
  ""runId"": ""…"",
  ""status"": ""Completed"",
  ""progress"": 100,
  ""output"": ""…what the job printed…"",
  ""startedAt"": ""2026-09-18T10:02:11+00:00"",
  ""completedAt"": ""2026-09-18T10:02:48+00:00""
}";

		static readonly DateTimeOffset Start = new DateTimeOffset(2026, 9, 18, 10, 2, 0, TimeSpan.Zero);

		static string Document(string status, int progress = 50) =>
			$@"{{""runId"":""r-1"",""status"":""{status}"",""progress"":{progress},""output"":null,""startedAt"":""2026-09-18T10:02:11+00:00"",""completedAt"":null}}";

		static Func<HttpRequestMessage, HttpResponseMessage> Ok(string json, string etag) => Reply(200, json, ("ETag", etag));

		static Func<HttpRequestMessage, HttpResponseMessage> NotModified(string etag) => Reply(304, null, ("ETag", etag));

		static DistenkaClientOptions Options() => new DistenkaClientOptions
		{
			BaseAddress = new Uri("https://api.example/"),
			Credential = new InvocationKeyCredential(InvokeKey),
		};

		/// <summary>A fake clock whose waits advance it instantly and are recorded.</summary>
		sealed class FakeTime
		{
			public DateTimeOffset Now { get; set; } = Start;

			public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

			public List<DateTimeOffset> Sent { get; } = new List<DateTimeOffset>();

			public Task Delay(TimeSpan wait, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				Delays.Add(wait);
				Now += wait;
				return Task.CompletedTask;
			}

			/// <summary>Wraps a step so the fake time of each request is recorded.</summary>
			public Func<HttpRequestMessage, HttpResponseMessage> At(Func<HttpRequestMessage, HttpResponseMessage> step) => request =>
			{
				Sent.Add(Now);
				return step(request);
			};
		}

		/// <summary>The public client, with the transport's retry waits recorded instead of slept.</summary>
		static (IDistenkaClient client, ScriptedHttpHandler handler, FakeTime time) Client(params Func<HttpRequestMessage, HttpResponseMessage>[] steps)
		{
			var handler = new ScriptedHttpHandler(steps);
			var time = new FakeTime();
			var transport = new HttpTransport(new HttpClient(handler), Options(), new RetryPolicy(3, TimeSpan.FromSeconds(60), time.Delay, () => 0.5));
			return (new DistenkaClient(transport), handler, time);
		}

		/// <summary>The result reader with a fake clock, for the polling loop.</summary>
		static (RunResultClient results, ScriptedHttpHandler handler, FakeTime time) Poller(Func<FakeTime, Func<HttpRequestMessage, HttpResponseMessage>[]> steps)
		{
			var time = new FakeTime();
			var handler = new ScriptedHttpHandler(steps(time).Select(time.At).ToArray());
			var transport = new HttpTransport(new HttpClient(handler), Options(), new RetryPolicy(3, TimeSpan.FromSeconds(60), time.Delay, () => 0.5));
			return (new RunResultClient(transport, () => time.Now, time.Delay), handler, time);
		}

		static RunCapabilityCredential Cap(DateTimeOffset? expiresAt = null) => new RunCapabilityCredential(Capability, expiresAt);

		static string IfNoneMatch(HttpRequestMessage request) =>
			request.Headers.TryGetValues("If-None-Match", out var values) ? values.Single() : null;

		[Fact]
		public async Task a_read_sends_the_capability_and_never_the_invocation_key()
		{
			var (client, handler, _) = Client(Ok(DocExample, "W/\"a1\""));

			var response = await client.Invocations.GetResultAsync(Cap());

			var request = handler.Requests.Single();
			request.Method.Should().Be(HttpMethod.Get);
			request.RequestUri.Should().Be(new Uri("https://api.example/api/runs/result"), "the capability names the run: no id in the route");
			request.Headers.Authorization.Scheme.Should().Be("Bearer");
			request.Headers.Authorization.Parameter.Should().Be(Capability);
			request.Headers.ToString().Should().NotContain(InvokeKey);
			IfNoneMatch(request).Should().BeNull("nothing is cached before the first read");

			response.NotModified.Should().BeFalse();
			response.ETag.Should().Be("W/\"a1\"");
			var result = response.Result;
			result.RunId.Should().Be("…");
			result.Status.Should().Be("Completed");
			result.State.Should().Be(RunState.Completed);
			result.Progress.Should().Be(100);
			result.Output.Should().Be("…what the job printed…");
			result.StartedAt.Should().Be(new DateTimeOffset(2026, 9, 18, 10, 2, 11, TimeSpan.Zero));
			result.CompletedAt.Should().Be(new DateTimeOffset(2026, 9, 18, 10, 2, 48, TimeSpan.Zero));
		}

		[Fact]
		public void the_doc_example_round_trips_through_the_client_context()
		{
			// Re-homed from DCS-023: RunResult registered on DistenkaJsonContext, six fields, State not on the wire.
			var result = JsonSerializer.Deserialize(DocExample, DistenkaJsonContext.Default.RunResult);
			var written = JsonDocument.Parse(JsonSerializer.Serialize(result, DistenkaJsonContext.Default.RunResult)).RootElement;
			var original = JsonDocument.Parse(DocExample).RootElement;

			written.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(original.EnumerateObject().Select(p => p.Name));
			foreach (var property in original.EnumerateObject())
			{
				var value = written.GetProperty(property.Name);
				if (property.Value.ValueKind == JsonValueKind.String)
					value.GetString().Should().Be(property.Value.GetString(), property.Name); // "…" may be written escaped
				else
					value.GetRawText().Should().Be(property.Value.GetRawText(), property.Name);
			}
		}

		[Fact]
		public void an_unknown_status_is_kept_and_parsed_as_Unknown()
		{
			var result = JsonSerializer.Deserialize(Document("Paused"), DistenkaJsonContext.Default.RunResult);

			result.Status.Should().Be("Paused");
			result.State.Should().Be(RunState.Unknown);
			result.State.IsTerminal().Should().BeFalse();
		}

		[Fact]
		public async Task a_304_is_not_modified_and_keeps_the_validator()
		{
			var (client, handler, _) = Client(NotModified("W/\"a1\""));

			var response = await client.Invocations.GetResultAsync(Cap(), "W/\"a1\"");

			IfNoneMatch(handler.Requests.Single()).Should().Be("W/\"a1\"");
			response.NotModified.Should().BeTrue();
			response.Result.Should().BeNull();
			response.ETag.Should().Be("W/\"a1\"");
		}

		[Fact]
		public async Task reads_200_304_304_200_give_two_documents_and_two_not_modified()
		{
			// AC 1.
			var (client, handler, _) = Client(
				Ok(Document("Running"), "W/\"a1\""),
				NotModified("W/\"a1\""),
				NotModified("W/\"a1\""),
				Ok(Document("Completed", 100), "W/\"b2\""));

			string etag = null;
			var responses = new List<RunResultResponse>();
			for (var i = 0; i < 4; i++)
			{
				var response = await client.Invocations.GetResultAsync(Cap(), etag);
				responses.Add(response);
				etag = response.ETag;
			}

			responses.Select(r => r.NotModified).Should().Equal(false, true, true, false);
			responses.Count(r => r.Result != null).Should().Be(2);
			responses[3].Result.State.Should().Be(RunState.Completed);
			handler.Requests.Select(IfNoneMatch).Should().Equal(null, "W/\"a1\"", "W/\"a1\"", "W/\"a1\"");
		}

		[Fact]
		public async Task a_401_surfaces_at_once()
		{
			// AC 2: expired, edited, retired-key or revoked — one answer, never retried.
			var (client, handler, time) = Client(Reply(401));

			var act = () => client.Invocations.GetResultAsync(Cap());

			(await act.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
			handler.Requests.Should().ContainSingle();
			time.Delays.Should().BeEmpty();
		}

		[Fact]
		public async Task a_429_on_a_read_honours_Retry_After()
		{
			// AC 2: the key's own result-poll window is full.
			var (client, handler, time) = Client(
				Reply(429, @"{""code"":""rate_limit_exceeded"",""error"":""Slow down.""}", ("Retry-After", "7")),
				Ok(DocExample, "W/\"a1\""));

			var response = await client.Invocations.GetResultAsync(Cap());

			response.Result.Should().NotBeNull();
			handler.Requests.Should().HaveCount(2);
			time.Delays.Should().Equal(TimeSpan.FromSeconds(7));
		}

		[Fact]
		public async Task capability_unavailable_is_retried()
		{
			var (client, handler, _) = Client(
				Reply(503, @"{""code"":""capability_unavailable"",""error"":""Try again.""}"),
				Ok(DocExample, "W/\"a1\""));

			(await client.Invocations.GetResultAsync(Cap())).Result.Should().NotBeNull();
			handler.Requests.Should().HaveCount(2);
		}

		[Theory]
		[InlineData("scope_violation")]
		[InlineData("run_capability_required")]
		public async Task a_403_is_terminal(string code)
		{
			var (client, handler, _) = Client(Reply(403, $@"{{""code"":""{code}"",""error"":""No.""}}"));

			var act = () => client.Invocations.GetResultAsync(Cap());

			(await act.Should().ThrowAsync<DistenkaApiException>()).Which.Code.Should().Be(code);
			handler.Requests.Should().ContainSingle();
		}

		[Fact]
		public async Task a_null_capability_is_refused()
		{
			var (client, _, _) = Client();

			await ((Func<Task>)(() => client.Invocations.GetResultAsync(null))).Should().ThrowAsync<ArgumentNullException>();
			await ((Func<Task>)(() => client.Invocations.WaitForResultAsync(null))).Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task waiting_sends_the_last_etag_backs_off_and_returns_the_final_document()
		{
			var (results, handler, time) = Poller(_ => new[]
			{
				Ok(Document("Running"), "W/\"a1\""),
				NotModified("W/\"a1\""),
				Ok(Document("Completed", 100), "W/\"b2\""),
			});

			var result = await results.WaitForResultAsync(Cap(), null, CancellationToken.None);

			result.State.Should().Be(RunState.Completed);
			result.Progress.Should().Be(100);
			handler.Requests.Select(IfNoneMatch).Should().Equal(null, "W/\"a1\"", "W/\"a1\"");
			time.Delays.Should().Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3) }, "2 s, then ×1.5 (ADR-007)");
		}

		[Theory]
		[InlineData("Failed")]
		[InlineData("Cancelled")]
		[InlineData("TimedOut")]
		[InlineData("Expired")]
		public async Task waiting_stops_on_every_terminal_status(string status)
		{
			var (results, handler, _) = Poller(_ => new[] { Ok(Document(status), "W/\"a1\"") });

			(await results.WaitForResultAsync(Cap(), null, CancellationToken.None)).Status.Should().Be(status);
			handler.Requests.Should().ContainSingle();
		}

		[Fact]
		public async Task waiting_treats_404_as_not_there_yet()
		{
			var (results, handler, _) = Poller(_ => new[]
			{
				Reply(404),
				Ok(Document("Completed", 100), "W/\"a1\""),
			});

			(await results.WaitForResultAsync(Cap(), null, CancellationToken.None)).State.Should().Be(RunState.Completed);
			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task waiting_honours_Retry_After_when_it_is_longer_than_the_interval()
		{
			var (results, _, time) = Poller(_ => new[]
			{
				Reply(429, @"{""code"":""rate_limit_exceeded"",""error"":""Slow down.""}", ("Retry-After", "7")),
				Reply(503, @"{""code"":""capability_unavailable"",""error"":""Try again.""}"),
				Ok(Document("Completed", 100), "W/\"a1\""),
			});

			await results.WaitForResultAsync(Cap(), null, CancellationToken.None);

			time.Delays.Should().Equal(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(3));
		}

		[Fact]
		public async Task waiting_rides_out_a_network_error()
		{
			var (results, handler, _) = Poller(_ => new Func<HttpRequestMessage, HttpResponseMessage>[]
			{
				_ => throw new HttpRequestException("connection reset"),
				Ok(Document("Completed", 100), "W/\"a1\""),
			});

			(await results.WaitForResultAsync(Cap(), null, CancellationToken.None)).State.Should().Be(RunState.Completed);
			handler.Requests.Should().HaveCount(2);
		}

		[Fact]
		public async Task waiting_surfaces_a_401_at_once()
		{
			// AC 2.
			var (results, handler, time) = Poller(_ => new[] { Ok(Document("Running"), "W/\"a1\""), Reply(401) });

			var act = () => results.WaitForResultAsync(Cap(), null, CancellationToken.None);

			(await act.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
			handler.Requests.Should().HaveCount(2);
			time.Delays.Should().ContainSingle();
		}

		[Fact]
		public async Task waiting_stops_at_the_capability_expiry()
		{
			var (results, handler, time) = Poller(_ => new[]
			{
				Ok(Document("Running"), "W/\"a1\""),
				NotModified("W/\"a1\""),
			});

			var act = () => results.WaitForResultAsync(Cap(Start + TimeSpan.FromSeconds(5)), null, CancellationToken.None);

			(await act.Should().ThrowAsync<TimeoutException>()).Which.Message.Should().Contain("capability expired").And.Contain("Running");
			handler.Requests.Should().HaveCount(2, "no request is sent at or after the expiry");
			time.Now.Should().Be(Start + TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task waiting_stops_at_the_timeout_when_it_comes_first()
		{
			var (results, handler, time) = Poller(_ => new[]
			{
				Ok(Document("Running"), "W/\"a1\""),
				NotModified("W/\"a1\""),
			});

			var act = () => results.WaitForResultAsync(Cap(Start + TimeSpan.FromHours(24)),
				new PollingOptions { Timeout = TimeSpan.FromSeconds(3) }, CancellationToken.None);

			(await act.Should().ThrowAsync<TimeoutException>()).Which.Message.Should().Contain("polling timeout");
			handler.Requests.Should().HaveCount(2);
			time.Delays.Should().Equal(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
		}

		[Fact]
		public async Task waiting_never_exceeds_the_poll_budget()
		{
			var (results, handler, time) = Poller(_ => new[]
			{
				Ok(Document("Running"), "W/\"a1\""),
				NotModified("W/\"a1\""),
				NotModified("W/\"a1\""),
				NotModified("W/\"a1\""),
				Ok(Document("Completed", 100), "W/\"b2\""),
			});

			var options = new PollingOptions
			{
				InitialInterval = TimeSpan.FromSeconds(1),
				MaxInterval = TimeSpan.FromSeconds(1),
				BackoffMultiplier = 1,
				MaxPollsPerMinute = 3,
			};

			(await results.WaitForResultAsync(Cap(), options, CancellationToken.None)).State.Should().Be(RunState.Completed);

			handler.Requests.Should().HaveCount(5);
			var offsets = time.Sent.Select(t => (t - Start).TotalSeconds).ToList();
			offsets.Should().Equal(0, 1, 2, 60, 61);
			for (var i = 0; i < time.Sent.Count; i++)
				time.Sent.Count(t => t >= time.Sent[i] && t < time.Sent[i] + TimeSpan.FromMinutes(1)).Should().BeLessOrEqualTo(3);
		}

		[Fact]
		public async Task waiting_can_be_cancelled()
		{
			using var cancellation = new CancellationTokenSource();
			var (results, _, _) = Poller(_ => new Func<HttpRequestMessage, HttpResponseMessage>[]
			{
				request =>
				{
					cancellation.Cancel();
					return Ok(Document("Running"), "W/\"a1\"")(request);
				},
			});

			var act = () => results.WaitForResultAsync(Cap(), null, cancellation.Token);

			await act.Should().ThrowAsync<OperationCanceledException>();
		}

		static PollingOptions InvalidOptions(int which) => which switch
		{
			0 => new PollingOptions { InitialInterval = TimeSpan.Zero },
			1 => new PollingOptions { InitialInterval = TimeSpan.FromSeconds(5), MaxInterval = TimeSpan.FromSeconds(1) },
			2 => new PollingOptions { BackoffMultiplier = 0.5 },
			3 => new PollingOptions { BackoffMultiplier = double.NaN },
			4 => new PollingOptions { Timeout = TimeSpan.Zero },
			_ => new PollingOptions { MaxPollsPerMinute = 0 },
		};

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(5)]
		public async Task invalid_polling_options_are_refused(int which)
		{
			var options = InvalidOptions(which);
			var (results, handler, _) = Poller(_ => Array.Empty<Func<HttpRequestMessage, HttpResponseMessage>>());

			var act = () => results.WaitForResultAsync(Cap(), options, CancellationToken.None);

			await act.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}
	}
}
