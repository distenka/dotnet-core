using Distenka.Client.Invocations;
using Distenka.Client.Runs;
using FluentAssertions;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-046 (re-homed from DCS-031): <see cref="RunDetails.TryParseSummary"/> / <see cref="RunResult.TryParseSummary"/>
	/// read the run summary a processor writes as the last line of its output.
	/// </summary>
	public class Run_output_summary
	{
		/// <summary>
		/// The host's golden summary (<c>Distenka.Tests/Scenarios/Running/Run_summary.cs</c>, <c>Run_summary_rules.Golden</c>) —
		/// change both together.
		/// </summary>
		const string Golden =
			"{\"distenkaSummary\":1,\"disposition\":\"Failed\",\"process\":\"Distenka.Diagnostics.DiagnosticProcessor\"," +
			"\"totalItems\":3,\"successfulItems\":2,\"failedItems\":1," +
			"\"categories\":[{\"category\":\"Rejected\",\"count\":1,\"successful\":false},{\"category\":\"Successful\",\"count\":2,\"successful\":true}]," +
			"\"categoriesNotListed\":0," +
			"\"methods\":{\"initializeAsync\":\"Successful\",\"getItemsAsync\":\"Successful\",\"finalizeAsync\":\"Successful\"}," +
			"\"failures\":[{\"id\":\"2\",\"category\":\"Rejected\",\"message\":\"Customer 2 has no plan\"}],\"failuresNotListed\":0," +
			"\"output\":{\"processed\":3},\"outputTruncated\":false,\"outputOmitted\":false,\"omitted\":0," +
			"\"startedAt\":\"2026-11-01T10:00:00.000Z\",\"completedAt\":\"2026-11-01T10:00:05.250Z\"}";

		/// <summary>What a run of a 2.0 processor looks like in <c>details.output</c>: plain lines, the ERROR: line, then the summary.</summary>
		const string FailedRunOutput =
			"INFO: Starting Distenka.Diagnostics.DiagnosticProcessor\nINFO: Initialized\nWARN: item '2' failed\nINFO: Disposition=Failed\n" +
			"ERROR: item failure threshold reached: 1 failed item(s), ItemFailureCountToStopProcess=1\n" + Golden;

		static RunDetails Details(string output) =>
			new RunDetails("Failed", null, null, null, null, null, null, null, null, null, null, null, output, null);

		[Fact]
		public void the_summary_line_is_read_from_the_last_line_of_the_details_output()
		{
			var summary = Details(FailedRunOutput).TryParseSummary();

			summary.Should().NotBeNull();
			summary.Version.Should().Be(1);
			summary.Disposition.Should().Be(RunDisposition.Failed);
			summary.DispositionName.Should().Be("Failed");
			summary.Process.Should().Be("Distenka.Diagnostics.DiagnosticProcessor");
			summary.TotalItems.Should().Be(3);
			summary.SuccessfulItems.Should().Be(2);
			summary.FailedItems.Should().Be(1);
			summary.Categories.Select(c => (c.Category, c.Count, c.Successful)).Should().Equal(new[] { ("Rejected", 1, false), ("Successful", 2, true) });
			summary.CategoriesNotListed.Should().Be(0);
			summary.Methods.Should().HaveCount(3).And.Contain("initializeAsync", "Successful").And.Contain("finalizeAsync", "Successful");
			var failure = summary.Failures.Should().ContainSingle().Which;
			failure.Id.Should().Be("2");
			failure.Category.Should().Be("Rejected");
			failure.Message.Should().Be("Customer 2 has no plan");
			summary.FailuresNotListed.Should().Be(0);
			summary.Output.Should().NotBeNull();
			summary.Output.Value.GetProperty("processed").GetInt32().Should().Be(3);
			summary.OutputTruncated.Should().BeFalse();
			summary.OutputOmitted.Should().BeFalse();
			summary.Omitted.Should().Be(0);
			summary.StartedAt.Should().Be(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
			summary.CompletedAt.Should().Be(new DateTimeOffset(2026, 11, 1, 10, 0, 5, 250, TimeSpan.Zero));
		}

		[Fact]
		public void legacy_output_without_a_summary_line_gives_null()
		{
			// The Run_details doc example: a processor's own JSON is the last line, without the distenkaSummary marker.
			Details("INFO: Starting Echo\nWARN: [Echo] slow item\nINFO: Disposition=Successful\n{\"disposition\":\"Successful\"}")
				.TryParseSummary().Should().BeNull();
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("  \n\r\n ")]
		[InlineData("INFO: Starting\nINFO: Disposition=Successful")]
		[InlineData("{\"distenkaSummary\":1,\"disposition\":")]
		[InlineData("{\"items\":[{\"distenkaSummary\":1}]}")]
		[InlineData("{\"distenkaSummary\":\"1\"}")]
		public void output_without_a_valid_summary_gives_null(string output)
		{
			RunOutputSummary.TryParse(output).Should().BeNull();
		}

		[Fact]
		public void a_summary_followed_by_a_line_break_and_CRLF_line_endings_is_still_read()
		{
			var output = FailedRunOutput.Replace("\n", "\r\n") + "\r\n";

			RunOutputSummary.TryParse(output).Should().NotBeNull().And.Subject.As<RunOutputSummary>().FailedItems.Should().Be(1);
		}

		[Fact]
		public void a_cancelled_run_without_counts_reads_with_empty_collections()
		{
			var line = "{\"distenkaSummary\":1,\"disposition\":\"Cancelled\",\"process\":null,\"totalItems\":null,\"successfulItems\":0,\"failedItems\":0," +
				"\"categories\":[],\"categoriesNotListed\":0,\"methods\":{},\"failures\":[],\"failuresNotListed\":0,\"output\":null," +
				"\"outputTruncated\":false,\"outputOmitted\":false,\"omitted\":0,\"startedAt\":null,\"completedAt\":null}";

			var summary = RunOutputSummary.TryParse("ERROR: run cancelled (host shutdown requested)\n" + line);

			summary.Disposition.Should().Be(RunDisposition.Cancelled);
			summary.Process.Should().BeNull();
			summary.TotalItems.Should().BeNull();
			summary.Categories.Should().BeEmpty();
			summary.Methods.Should().BeEmpty();
			summary.Failures.Should().BeEmpty();
			summary.Output.Should().BeNull();
			summary.StartedAt.Should().BeNull();
		}

		[Fact]
		public void a_newer_summary_version_with_unknown_members_and_disposition_still_reads()
		{
			var summary = RunOutputSummary.TryParse("{\"distenkaSummary\":2,\"disposition\":\"Paused\",\"successfulItems\":4,\"newThing\":{\"a\":1}}");

			summary.Version.Should().Be(2);
			summary.Disposition.Should().Be(RunDisposition.Unknown);
			summary.DispositionName.Should().Be("Paused");
			summary.SuccessfulItems.Should().Be(4);
			summary.Categories.Should().BeEmpty("members a summary omits read as empty, never null");
		}

		[Fact]
		public void a_text_output_and_the_truncation_flag_are_read()
		{
			var summary = RunOutputSummary.TryParse(
				"{\"distenkaSummary\":1,\"disposition\":\"Successful\",\"output\":\"xxxx…\",\"outputTruncated\":true}");

			summary.Output.Value.ValueKind.Should().Be(JsonValueKind.String);
			summary.Output.Value.GetString().Should().Be("xxxx…");
			summary.OutputTruncated.Should().BeTrue();
		}

		[Fact]
		public void the_capability_result_output_is_read_the_same_way()
		{
			var result = new RunResult("r-1", "Failed", 100, FailedRunOutput, null, null);

			result.TryParseSummary().Should().NotBeNull().And.Subject.As<RunOutputSummary>().Disposition.Should().Be(RunDisposition.Failed);
			new RunResult("r-2", "Running", 10, null, null, null).TryParseSummary().Should().BeNull("there is no output yet");
		}
	}
}
