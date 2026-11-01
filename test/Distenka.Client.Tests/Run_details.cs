using Distenka.Client.Runs;
using Distenka.Client.Serialization;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-031: <c>GET /api/runs/{runId}/details</c> and the execution-log contract (AD-13): <c>timeline</c> + <c>output</c>.
	/// Field names from docs/api-run-submission.md §Reading the run back; timeline items from the platform's
	/// <c>RunDto.Step</c> (<c>Distenka.Core.Kernel/DTOs/RunDto.cs:93-98</c> @ 4c3cf2c, as recorded in the backlog): structured
	/// entries carry <c>level</c> INFO/WARN/ERROR, legacy plain-text entries carry <c>level: null</c>.
	/// </summary>
	public class Run_details
	{
		const string Structured = @"{
  ""state"": ""Completed"", ""status"": ""Completed"",
  ""createdAt"": ""2026-10-01T02:40:00+00:00"", ""queuedAt"": ""2026-10-01T02:40:01+00:00"",
  ""dispatchingAt"": ""2026-10-01T02:40:05+00:00"", ""startedAt"": ""2026-10-01T02:40:11+00:00"",
  ""completedAt"": ""2026-10-01T02:40:16+00:00"", ""deadlineAt"": null, ""cancelRequestedAt"": null,
  ""attempt"": 1, ""invokedByKeyPrefix"": null, ""totalExecutionMs"": 5000,
  ""timeline"": [
    { ""timestamp"": ""2026-10-01T02:40:11.5+00:00"", ""message"": ""Starting Echo"", ""level"": ""INFO"", ""elapsed"": 500 },
    { ""timestamp"": ""2026-10-01T02:40:14+00:00"", ""message"": ""[Echo] slow item"", ""level"": ""WARN"", ""elapsed"": 3000 },
    { ""timestamp"": ""2026-10-01T02:40:16+00:00"", ""message"": ""Disposition=Successful"", ""level"": ""INFO"", ""elapsed"": 5000.25 }
  ],
  ""output"": ""INFO: Starting Echo\nWARN: [Echo] slow item\nINFO: Disposition=Successful\n{\""disposition\"":\""Successful\""}""
}";

		const string Legacy = @"{
  ""state"": ""Completed"", ""status"": ""Completed"",
  ""timeline"": [
    { ""timestamp"": null, ""message"": ""line one"", ""level"": null, ""elapsed"": null },
    { ""timestamp"": null, ""message"": ""line two"", ""level"": null, ""elapsed"": null }
  ],
  ""output"": ""line one\nline two""
}";

		static (IDistenkaClient client, ScriptedHttpHandler handler) Create(ScriptedHttpHandler handler) =>
			(new DistenkaClient(new HttpClient(handler), new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example/"),
				Credential = new Authentication.AccountKeyCredential("DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"),
				MaxRetryDelay = TimeSpan.FromSeconds(1),
			}), handler);

		[Fact]
		public async Task GetDetailsAsync_reads_the_owners_details_route()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(200, Structured)));

			var details = await client.Runs.GetDetailsAsync("r-8f2c");

			var request = handler.Requests.Single();
			request.Method.Should().Be(HttpMethod.Get);
			request.RequestUri.Should().Be(new Uri("https://api.example/api/runs/r-8f2c/details"));
			details.State.Should().Be(RunState.Completed);
			details.TotalExecutionMs.Should().Be(5000);
		}

		[Fact]
		public async Task the_run_id_is_escaped_in_the_path()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(200, Structured)));

			await client.Runs.GetDetailsAsync("a/b c");

			handler.Requests.Single().RequestUri.AbsoluteUri.Should().Be("https://api.example/api/runs/a%2Fb%20c/details");
		}

		[Fact]
		public async Task stages_not_reached_stay_null()
		{
			// AC 1.
			var (client, _) = Create(new ScriptedHttpHandler(Reply(200,
				@"{""state"":""Queued"",""status"":""Pending"",""createdAt"":""2026-10-01T02:40:00+00:00"",""queuedAt"":""2026-10-01T02:40:01+00:00"",""dispatchingAt"":null,""startedAt"":null,""completedAt"":null}")));

			var details = await client.Runs.GetDetailsAsync("r-1");

			details.State.Should().Be(RunState.Queued);
			details.QueuedAt.Should().NotBeNull();
			details.DispatchingAt.Should().BeNull();
			details.StartedAt.Should().BeNull();
			details.CompletedAt.Should().BeNull();
			details.Timeline.Should().BeEmpty("no timeline yet is an empty list, not null");
			details.Output.Should().BeNull();
		}

		[Fact]
		public void structured_timeline_entries_carry_level_and_elapsed()
		{
			// AC 2, structured-log path.
			var details = JsonSerializer.Deserialize(Structured, DistenkaJsonContext.Default.RunDetails);

			details.Timeline.Should().HaveCount(3);
			details.Timeline.Select(e => e.Level).Should().Equal(RunLogLevel.Info, RunLogLevel.Warn, RunLogLevel.Info);
			details.Timeline.Select(e => e.LevelName).Should().Equal("INFO", "WARN", "INFO");
			details.Timeline[1].Message.Should().Be("[Echo] slow item");
			details.Timeline[1].Elapsed.Should().Be(TimeSpan.FromSeconds(3));
			details.Timeline[2].Elapsed.Should().Be(TimeSpan.FromTicks(50_002_500)); // 5000.25 ms
			details.Timeline[0].Timestamp.Should().Be(new DateTimeOffset(2026, 10, 1, 2, 40, 11, 500, TimeSpan.Zero));
		}

		[Fact]
		public void legacy_plain_text_entries_have_no_level()
		{
			// AC 2, legacy path (SplitPlainTextIntoTimeline: level null).
			var details = JsonSerializer.Deserialize(Legacy, DistenkaJsonContext.Default.RunDetails);

			details.Timeline.Should().HaveCount(2).And.OnlyContain(e => e.Level == null && e.LevelName == null && e.Elapsed == null && e.Timestamp == null);
			details.Timeline.Select(e => e.Message).Should().Equal("line one", "line two");
		}

		[Theory]
		[InlineData("ERROR", RunLogLevel.Error)]
		[InlineData("error", RunLogLevel.Error)]
		[InlineData("WARNING", RunLogLevel.Warn)]
		[InlineData("DEBUG", RunLogLevel.Unknown)]
		public void levels_are_read_tolerantly(string level, RunLogLevel expected) =>
			new RunTimelineEntry(null, "m", level, null).Level.Should().Be(expected);

		[Fact]
		public void timeline_entries_round_trip()
		{
			foreach (var json in new[] { Structured, Legacy })
			{
				var details = JsonSerializer.Deserialize(json, DistenkaJsonContext.Default.RunDetails);
				var again = JsonSerializer.Deserialize(JsonSerializer.Serialize(details, DistenkaJsonContext.Default.RunDetails), DistenkaJsonContext.Default.RunDetails);

				again.Timeline.Should().BeEquivalentTo(details.Timeline);
				again.Output.Should().Be(details.Output);
			}
		}

		[Fact]
		public void elapsed_is_written_back_as_milliseconds()
		{
			var details = JsonSerializer.Deserialize(Structured, DistenkaJsonContext.Default.RunDetails);

			var written = JsonSerializer.Serialize(details, DistenkaJsonContext.Default.RunDetails);

			written.Should().Contain(@"""elapsed"":3000").And.Contain(@"""elapsed"":5000.25").And.Contain(@"""level"":""WARN""");
		}

		[Fact]
		public void a_non_numeric_elapsed_is_refused()
		{
			var read = () => JsonSerializer.Deserialize(@"{""timeline"":[{""elapsed"":""3s""}]}", DistenkaJsonContext.Default.RunDetails);

			read.Should().Throw<JsonException>();
		}

		[Fact]
		public async Task output_is_kept_verbatim_including_the_last_line()
		{
			// AC 3, first half (TryParseSummary is re-homed to DCS-046, which defines the summary line).
			var (client, _) = Create(new ScriptedHttpHandler(Reply(200, Structured)));

			var details = await client.Runs.GetDetailsAsync("r-1");

			details.Output.Should().Be("INFO: Starting Echo\nWARN: [Echo] slow item\nINFO: Disposition=Successful\n{\"disposition\":\"Successful\"}");
			details.Output.Split('\n').Last().Should().Be("{\"disposition\":\"Successful\"}");
		}

		[Fact]
		public async Task a_404_is_an_exception_without_a_code()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(404)));

			var read = () => client.Runs.GetDetailsAsync("not-mine");

			var thrown = (await read.Should().ThrowAsync<DistenkaApiException>()).Which;
			thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
			thrown.Code.Should().BeNull();
			handler.Requests.Should().HaveCount(1, "404 is not retried");
		}

		[Fact]
		public async Task TryGetDetailsAsync_returns_null_for_a_404()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(404)));

			(await client.Runs.TryGetDetailsAsync("purged")).Should().BeNull();
		}

		[Fact]
		public async Task TryGetDetailsAsync_still_throws_other_failures()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(401)));

			var read = () => client.Runs.TryGetDetailsAsync("r-1");

			(await read.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task a_transient_failure_is_retried_because_the_read_is_a_GET()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(503), Reply(200, Structured)));

			var details = await client.Runs.GetDetailsAsync("r-1");

			details.State.Should().Be(RunState.Completed);
			handler.Requests.Should().HaveCount(2);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" ")]
		public async Task a_run_id_is_required(string runId)
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var read = () => client.Runs.GetDetailsAsync(runId);

			await read.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}
	}
}
