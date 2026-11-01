using Distenka.Client.Runs;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-033: <c>POST /api/runs/{runId}/cancel</c> (docs/api-run-cancellation.md). The <c>200</c> body is the doc's example
	/// verbatim; the <c>409 run_already_ended</c> body's state member is not named by the docs — see <c>RunsClient.EndedState</c>.
	/// </summary>
	public class Run_cancellation
	{
		const string Cancelled = @"{ ""runId"": ""…"", ""status"": ""Cancellation requested"", ""cancelRequestedAt"": ""2026-10-01T02:40:11.123456+00:00"" }";

		static readonly DateTimeOffset RequestedAt = DateTimeOffset.Parse("2026-10-01T02:40:11.123456+00:00", System.Globalization.CultureInfo.InvariantCulture);

		static (IDistenkaClient client, ScriptedHttpHandler handler) Create(ScriptedHttpHandler handler) =>
			(new DistenkaClient(new HttpClient(handler), new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example/"),
				Credential = new Authentication.AccountKeyCredential("DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"),
				MaxRetryDelay = TimeSpan.FromSeconds(1),
			}), handler);

		static string AlreadyEnded(string member, string state) =>
			$@"{{""code"":""run_already_ended"",""error"":""The run already ended."",""{member}"":""{state}""}}";

		[Fact]
		public async Task cancel_posts_to_the_runs_cancel_route_without_a_body()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(200, Cancelled)));

			await client.Runs.CancelAsync("r-8f2c");

			var request = handler.Requests.Single();
			request.Method.Should().Be(HttpMethod.Post);
			request.RequestUri.Should().Be(new Uri("https://api.example/api/runs/r-8f2c/cancel"));
			handler.Bodies.Single().Should().BeNull();
		}

		[Fact]
		public async Task a_200_is_a_standing_cancellation_request()
		{
			// AC 1, first half.
			var (client, _) = Create(new ScriptedHttpHandler(Reply(200, Cancelled)));

			var result = await client.Runs.CancelAsync("r-1");

			result.AlreadyEnded.Should().BeFalse();
			result.EndedState.Should().BeNull();
			result.Status.Should().Be("Cancellation requested");
			result.CancelRequestedAt.Should().Be(RequestedAt);
		}

		[Fact]
		public async Task a_repeated_cancel_returns_the_same_time()
		{
			// AC 1, second half: "The first request's time is kept and never moves. A repeat gets the same body."
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(200, Cancelled), Reply(200, Cancelled)));

			var first = await client.Runs.CancelAsync("r-1");
			var second = await client.Runs.CancelAsync("r-1");

			second.CancelRequestedAt.Should().Be(first.CancelRequestedAt);
			handler.Requests.Should().HaveCount(2);
		}

		[Theory]
		[InlineData("state", "Completed", RunState.Completed)]
		[InlineData("state", "Cancelled", RunState.Cancelled)]
		[InlineData("endedState", "Expired", RunState.Expired)]
		[InlineData("finalState", "TimedOut", RunState.TimedOut)]
		[InlineData("state", "Archived", RunState.Unknown)]
		public async Task run_already_ended_is_a_typed_outcome(string member, string state, RunState expected)
		{
			// AC 2.
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(409, AlreadyEnded(member, state))));

			var result = await client.Runs.CancelAsync("r-1");

			result.AlreadyEnded.Should().BeTrue();
			result.EndedState.Should().Be(expected);
			result.RunId.Should().Be("r-1");
			result.CancelRequestedAt.Should().BeNull();
			handler.Requests.Should().HaveCount(1, "an ended run is terminal — not retried");
		}

		[Fact]
		public async Task run_already_ended_without_a_state_member_has_no_ended_state()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(409, @"{""code"":""run_already_ended"",""error"":""The run already ended.""}")));

			var result = await client.Runs.CancelAsync("r-1");

			result.AlreadyEnded.Should().BeTrue();
			result.EndedState.Should().BeNull();
		}

		[Fact]
		public async Task ThrowOnAlreadyEnded_rethrows_the_409()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(409, AlreadyEnded("state", "Completed"))));

			var cancel = () => client.Runs.CancelAsync("r-1", new CancelRunOptions { ThrowOnAlreadyEnded = true });

			(await cancel.Should().ThrowAsync<DistenkaApiException>()).Which.Code.Should().Be(DistenkaErrorCodes.RunAlreadyEnded);
		}

		[Fact]
		public async Task other_409s_are_not_mistaken_for_an_ended_run()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(409, @"{""code"":""something_else"",""error"":""x""}")));

			var cancel = () => client.Runs.CancelAsync("r-1");

			(await cancel.Should().ThrowAsync<DistenkaApiException>()).Which.Code.Should().Be("something_else");
		}

		[Fact]
		public async Task a_404_is_an_exception()
		{
			var (client, _) = Create(new ScriptedHttpHandler(Reply(404)));

			var cancel = () => client.Runs.CancelAsync("not-mine");

			(await cancel.Should().ThrowAsync<DistenkaApiException>()).Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
		}

		[Fact]
		public async Task a_cancel_is_retried_after_a_transient_failure_because_repeating_it_is_safe()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(503), Reply(200, Cancelled)));

			var result = await client.Runs.CancelAsync("r-1");

			result.CancelRequestedAt.Should().Be(RequestedAt);
			handler.Requests.Should().HaveCount(2);
			handler.Requests.Should().OnlyContain(r => !r.Headers.Contains("Idempotency-Key"));
		}

		[Fact]
		public async Task the_run_id_is_escaped()
		{
			var (client, handler) = Create(new ScriptedHttpHandler(Reply(200, Cancelled)));

			await client.Runs.CancelAsync("a/b");

			handler.Requests.Single().RequestUri.AbsoluteUri.Should().Be("https://api.example/api/runs/a%2Fb/cancel");
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" ")]
		public async Task a_run_id_is_required(string runId)
		{
			var (client, handler) = Create(new ScriptedHttpHandler());

			var cancel = () => client.Runs.CancelAsync(runId);

			await cancel.Should().ThrowAsync<ArgumentException>();
			handler.Requests.Should().BeEmpty();
		}

		[Fact]
		public void the_extra_outcomes_are_not_part_of_the_JSON_contract()
		{
			var result = new CancelResult("r-1", null, null, alreadyEnded: true, endedState: RunState.Completed);

			System.Text.Json.JsonSerializer.Serialize(result, Serialization.DistenkaJsonContext.Default.CancelResult)
				.Should().Be(@"{""runId"":""r-1""}");
		}
	}
}
