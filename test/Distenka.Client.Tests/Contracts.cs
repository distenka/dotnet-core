using Distenka.Client.Invocations;
using Distenka.Client.Runs;
using Distenka.Client.Serialization;
using FluentAssertions;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-023: the client contracts and <see cref="DistenkaJsonContext"/> (ADR-008). Examples marked "verbatim" are copied
	/// from the platform docs (docs/api-run-submission.md, api-invocation.md, api-run-cancellation.md, owner-supplied
	/// 2026-11-01); where a doc gives only a field table or a schematic body, the JSON is built from it and says so.
	/// </summary>
	public class Contracts
	{
		static DistenkaJsonContext Context => DistenkaJsonContext.Default;

		static void ShouldRoundTrip<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
		{
			var value = JsonSerializer.Deserialize(json, typeInfo);
			var written = JsonSerializer.Serialize(value, typeInfo);

			using var expected = JsonDocument.Parse(json);
			using var actual = JsonDocument.Parse(written);
			SameJson(expected.RootElement, actual.RootElement).Should().BeTrue($"round-trip of {json} gave {written}");
		}

		/// <summary>Structural equality: object members in any order, arrays in order, scalars by their raw JSON text.</summary>
		static bool SameJson(JsonElement a, JsonElement b)
		{
			if (a.ValueKind != b.ValueKind)
				return false;

			switch (a.ValueKind)
			{
				case JsonValueKind.Object:
					var left = a.EnumerateObject().ToList();
					var right = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
					return left.Count == right.Count && left.All(p => right.TryGetValue(p.Name, out var v) && SameJson(p.Value, v));
				case JsonValueKind.Array:
					var l = a.EnumerateArray().ToList();
					var r = b.EnumerateArray().ToList();
					return l.Count == r.Count && l.Zip(r, SameJson).All(x => x);
				case JsonValueKind.String:
					return a.GetString() == b.GetString();
				default:
					return a.GetRawText() == b.GetRawText();
			}
		}

		// ---- Verbatim doc examples ---------------------------------------------------------------------------------------

		const string InvokeAccepted = @"{
  ""runId"": ""…"",
  ""status"": ""Pending"",
  ""resultCapability"": ""drc1.…"",
  ""resultCapabilityExpiresAt"": ""2026-09-19T10:02:11+00:00""
}"; // docs/api-invocation.md §The response, verbatim

		const string CancelAccepted = @"{ ""runId"": ""…"", ""status"": ""Cancellation requested"", ""cancelRequestedAt"": ""2026-10-01T02:40:11.123456+00:00"" }";
		// docs/api-run-cancellation.md §The responses, verbatim

		const string Summary = @"{
  ""byState"": { ""Accepted"": 0, ""Queued"": 4, ""Dispatching"": 1, ""Running"": 2,
               ""Completed"": 310, ""Failed"": 7, ""Cancelled"": 1, ""TimedOut"": 0, ""Expired"": 2 },
  ""last24h"": { ""accepted"": 41, ""completed"": 36, ""failed"": 2, ""timedOut"": 0, ""expired"": 0, ""cancelled"": 1 },
  ""asOf"": ""2026-10-20T06:00:00+00:00""
}"; // docs/api-run-submission.md §Counting your runs, verbatim

		[Fact]
		public void invocation_accepted_round_trips_the_documented_example()
		{
			ShouldRoundTrip(InvokeAccepted, Context.InvocationAccepted);

			var value = JsonSerializer.Deserialize(InvokeAccepted, Context.InvocationAccepted);
			value.RunId.Should().Be("…");
			value.Status.Should().Be("Pending");
			value.ResultCapability.Should().NotBeNull();
			value.ResultCapability.ToString().Should().Be("drc1.…", "the capability is held as a credential, never a public string");
			value.ResultCapabilityExpiresAt.Should().Be(new DateTimeOffset(2026, 9, 19, 10, 2, 11, TimeSpan.Zero));
		}

		[Fact]
		public async Task the_deserialized_capability_is_a_usable_credential()
		{
			var value = JsonSerializer.Deserialize(InvokeAccepted, Context.InvocationAccepted);

			(await value.ResultCapability.GetBearerTokenAsync(CancellationToken.None)).Should().Be("drc1.…");
		}

		[Fact]
		public void a_null_capability_is_null()
		{
			// docs: "resultCapability can be null — the deployment has no capability signing key configured".
			var value = JsonSerializer.Deserialize(@"{""runId"":""r"",""status"":""Pending"",""resultCapability"":null,""resultCapabilityExpiresAt"":null}", Context.InvocationAccepted);

			value.ResultCapability.Should().BeNull();
			value.ResultCapabilityExpiresAt.Should().BeNull();
		}

		[Fact]
		public void a_malformed_capability_fails_without_echoing_it()
		{
			var read = () => JsonSerializer.Deserialize(@"{""runId"":""r"",""resultCapability"":""DK-SECRET0123456789""}", Context.InvocationAccepted);

			read.Should().Throw<JsonException>().Which.Message.Should().NotContain("SECRET");
		}

		[Fact]
		public void cancel_result_round_trips_the_documented_example()
		{
			ShouldRoundTrip(CancelAccepted, Context.CancelResult);

			var value = JsonSerializer.Deserialize(CancelAccepted, Context.CancelResult);
			value.Status.Should().Be("Cancellation requested");
			value.CancelRequestedAt.Should().Be(DateTimeOffset.Parse("2026-10-01T02:40:11.123456+00:00", System.Globalization.CultureInfo.InvariantCulture));
		}

		[Fact]
		public void run_summary_round_trips_the_documented_example()
		{
			ShouldRoundTrip(Summary, Context.RunSummary);

			var value = JsonSerializer.Deserialize(Summary, Context.RunSummary);
			value.ByState.Should().HaveCount(9).And.Contain("Completed", 310L).And.Contain("TimedOut", 0L);
			value.Last24h.Accepted.Should().Be(41);
			value.Last24h.TimedOut.Should().Be(0);
			value.Last24h.Cancelled.Should().Be(1);
			value.AsOf.Should().Be(new DateTimeOffset(2026, 10, 20, 6, 0, 0, TimeSpan.Zero));
		}

		[Fact]
		public void run_summary_keeps_a_state_name_the_SDK_does_not_know()
		{
			var value = JsonSerializer.Deserialize(@"{""byState"":{""Queued"":1,""Paused"":3}}", Context.RunSummary);

			value.ByState.Should().Contain("Paused", 3L);
		}

		// ---- Schematic / table-only docs ---------------------------------------------------------------------------------

		[Fact]
		public void run_accepted_round_trips()
		{
			// docs/api-run-submission.md gives the body as `{ runId, processorId, trackingUrl }`; values are illustrative
			// (processorId from the doc's request example).
			const string json = @"{""runId"":""r-8f2c"",""processorId"":""etl-loyalty"",""trackingUrl"":""https://api.example/api/runs/r-8f2c/details""}";

			ShouldRoundTrip(json, Context.RunAccepted);
			JsonSerializer.Deserialize(json, Context.RunAccepted).ProcessorId.Should().Be("etl-loyalty");
		}

		[Fact]
		public void run_details_round_trips_every_documented_field()
		{
			// Built from the field table in docs/api-run-submission.md §Reading the run back (the doc has no JSON example).
			const string json = @"{
  ""state"": ""Completed"", ""status"": ""Completed"",
  ""createdAt"": ""2026-10-01T02:40:00+00:00"", ""queuedAt"": ""2026-10-01T02:40:01+00:00"",
  ""dispatchingAt"": ""2026-10-01T02:40:05+00:00"", ""startedAt"": ""2026-10-01T02:40:11.123456+00:00"",
  ""completedAt"": ""2026-10-01T02:41:11.123456+00:00"", ""deadlineAt"": ""2026-10-01T21:48:40+00:00"",
  ""cancelRequestedAt"": ""2026-10-01T02:41:00+00:00"", ""attempt"": 1, ""invokedByKeyPrefix"": ""DKI-01234567"",
  ""totalExecutionMs"": 60000, ""output"": ""INFO: done"",
  ""timeline"": [ { ""timestamp"": ""2026-10-01T02:40:12+00:00"", ""message"": ""done"", ""level"": ""INFO"", ""elapsed"": 1000 } ]
}"; // timeline item shape: platform RunDto.Step (DCS-031)

			ShouldRoundTrip(json, Context.RunDetails);

			var value = JsonSerializer.Deserialize(json, Context.RunDetails);
			value.State.Should().Be(RunState.Completed);
			value.Attempt.Should().Be(1);
			value.TotalExecutionMs.Should().Be(60000);
			value.InvokedByKeyPrefix.Should().Be("DKI-01234567");
			value.Output.Should().Be("INFO: done");
		}

		[Fact]
		public void run_details_stages_not_reached_are_null_and_omitted_on_write()
		{
			const string json = @"{""state"":""Queued"",""status"":""Pending"",""createdAt"":""2026-10-01T02:40:00+00:00"",""queuedAt"":null,""startedAt"":null}";

			var value = JsonSerializer.Deserialize(json, Context.RunDetails);

			value.State.Should().Be(RunState.Queued);
			value.Status.Should().Be("Pending", "status is the projection's word and may differ from the state");
			value.StartedAt.Should().BeNull();
			JsonSerializer.Serialize(value, Context.RunDetails).Should().NotContain("startedAt", "null members are omitted (ADR-008)");
		}

		[Fact]
		public void unknown_members_are_ignored()
		{
			var value = JsonSerializer.Deserialize(@"{""runId"":""r"",""processorId"":""p"",""trackingUrl"":null,""somethingNew"":{""x"":1}}", Context.RunAccepted);

			value.RunId.Should().Be("r");
		}

		// ---- RunState (AC 2, AC 3) ----------------------------------------------------------------------------------------

		[Fact]
		public void an_unknown_state_is_Unknown_and_its_name_is_kept()
		{
			// AC 2 — the raw name is kept on RunDetails.StateName (not in Status, which is a separate documented field).
			var value = JsonSerializer.Deserialize(@"{""state"":""Paused"",""status"":""Pending""}", Context.RunDetails);

			value.State.Should().Be(RunState.Unknown);
			value.StateName.Should().Be("Paused");
			value.Status.Should().Be("Pending");
			JsonSerializer.Serialize(value, Context.RunDetails).Should().Contain(@"""state"":""Paused""", "the raw name round-trips");
		}

		[Theory]
		[InlineData("\"Accepted\"", RunState.Accepted)]
		[InlineData("\"Queued\"", RunState.Queued)]
		[InlineData("\"Dispatching\"", RunState.Dispatching)]
		[InlineData("\"Running\"", RunState.Running)]
		[InlineData("\"Completed\"", RunState.Completed)]
		[InlineData("\"Failed\"", RunState.Failed)]
		[InlineData("\"Cancelled\"", RunState.Cancelled)]
		[InlineData("\"TimedOut\"", RunState.TimedOut)]
		[InlineData("\"Expired\"", RunState.Expired)]
		[InlineData("\"timedout\"", RunState.TimedOut)]
		[InlineData("\"Paused\"", RunState.Unknown)]
		[InlineData("\"3\"", RunState.Unknown)]
		[InlineData("\"\"", RunState.Unknown)]
		public void run_states_are_read_tolerantly(string json, RunState expected) =>
			JsonSerializer.Deserialize(json, Context.RunState).Should().Be(expected);

		[Fact]
		public void a_numeric_state_is_refused()
		{
			var read = () => JsonSerializer.Deserialize("3", Context.RunState);

			read.Should().Throw<JsonException>();
		}

		[Fact]
		public void run_states_are_written_by_name()
		{
			JsonSerializer.Serialize(RunState.TimedOut, Context.RunState).Should().Be("\"TimedOut\"");
			JsonSerializer.Serialize(RunState.Unknown, Context.RunState).Should().Be("\"Unknown\"");
		}

		[Fact]
		public void terminal_states_are_exactly_the_five_ending_states()
		{
			// AC 3.
			Enum.GetValues(typeof(RunState)).Cast<RunState>().Where(s => s.IsTerminal())
				.Should().BeEquivalentTo(new[] { RunState.Completed, RunState.Failed, RunState.Cancelled, RunState.TimedOut, RunState.Expired });
		}

		[Fact]
		public void the_nine_documented_states_and_Unknown_are_the_whole_enum()
		{
			Enum.GetNames(typeof(RunState)).Should().BeEquivalentTo(
				"Unknown", "Accepted", "Queued", "Dispatching", "Running", "Completed", "Failed", "Cancelled", "TimedOut", "Expired");
		}

		// ---- Context options (ADR-008 §2.1.4) ----------------------------------------------------------------------------

		[Fact]
		public void numbers_in_strings_are_refused()
		{
			var read = () => JsonSerializer.Deserialize(@"{""state"":""Running"",""attempt"":""1""}", Context.RunDetails);

			read.Should().Throw<JsonException>("NumberHandling is Strict, overriding the Web default");
		}

		[Fact]
		public void member_names_are_read_case_insensitively_and_written_camelCase()
		{
			var value = JsonSerializer.Deserialize(@"{""RunId"":""r"",""ProcessorId"":""p"",""TrackingUrl"":""t""}", Context.RunAccepted);

			value.ProcessorId.Should().Be("p");
			JsonSerializer.Serialize(value, Context.RunAccepted).Should().Be(@"{""runId"":""r"",""processorId"":""p"",""trackingUrl"":""t""}");
		}

		[Fact]
		public void the_context_uses_the_ADR_008_options()
		{
			var options = Context.Options;

			options.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
			options.PropertyNameCaseInsensitive.Should().BeTrue();
			options.NumberHandling.Should().Be(System.Text.Json.Serialization.JsonNumberHandling.Strict);
			options.DefaultIgnoreCondition.Should().Be(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
			options.UnmappedMemberHandling.Should().Be(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip);
		}
	}
}
