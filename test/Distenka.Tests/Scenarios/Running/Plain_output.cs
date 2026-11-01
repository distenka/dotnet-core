using FluentAssertions;
using Distenka.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>
	/// DCS-041 / ADR-009 §2.1–2.2 / ADR-004 §2.3–2.4: with stdout redirected (always the case under the executor) `run`
	/// writes only `LEVEL: message` lines — no ANSI sequences, spinners or markup — and keeps the SDK's own lines bounded.
	/// Process-level: spawns the built Distenka.Diagnostics with stdout redirected.
	/// </summary>
	public class Plain_output
	{
		const string ProcessKey = "\"process\": \"Distenka.Diagnostics.DiagnosticProcessor\"";

		static readonly Regex LevelLine = new Regex(@"^(DEBUG|INFO|WARN|ERROR): \S", RegexOptions.CultureInvariant);

		static void ShouldBePlainLinesOnly(ProcessRun run)
		{
			run.Stdout.Should().NotBeEmpty(run.ToString());
			// Every line is LEVEL: message, except the last: the run summary (DCS-046).
			run.Stdout.Take(run.Stdout.Count - 1).Should().OnlyContain(l => LevelLine.IsMatch(l), run.ToString());
			run.Summary.Should().NotBeNull("the last line is the run summary; " + run);
			run.Stdout.Should().NotContain(l => l.Contains('\u001b'), "no ANSI escape sequences; " + run);
			run.Stderr.Should().BeEmpty(run.ToString());
		}

		[Fact]
		public async Task successful_run_writes_only_level_prefixed_lines()
		{
			var run = await ProcessorProcess.RunDiagnosticsAsync($"{{ {ProcessKey}, \"numberOfItems\": 5, \"millisecondDelayPerItem\": 0 }}");

			run.ExitCode.Should().Be(0, run.ToString());
			ShouldBePlainLinesOnly(run);
			run.Stdout.Should().ContainInOrder(new[]
			{
				"INFO: Starting Distenka.Diagnostics.DiagnosticProcessor",
				"INFO: Initialized",
				"INFO: Processing 5 items",
				"INFO: Processed 5/5 items (5 successful, 0 failed)",
				"INFO: Item results: 5 Successful",
				"INFO: Finalized",
				"INFO: Disposition=Successful"
			}, run.ToString());
			run.Stdout.Where(ExecutorRules.IsErrorStdoutLine).Should().BeEmpty(run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task plain_flag_is_accepted()
		{
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 2, \"millisecondDelayPerItem\": 0 }}", extraArgs: new[] { "--plain" });

			run.ExitCode.Should().Be(0, run.ToString());
			ShouldBePlainLinesOnly(run);
			run.Stdout[run.Stdout.Count - 2].Should().Be("INFO: Disposition=Successful", run.ToString());
			run.Summary.Value<string>("disposition").Should().Be("Successful", run.ToString());
		}

		[Fact]
		public async Task failed_run_writes_level_prefixed_lines_and_exactly_one_ERROR_line()
		{
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInInitializeAsync\": true }}");

			run.ExitCode.Should().Be(1, run.ToString());
			ShouldBePlainLinesOnly(run);
			run.ErrorLines.Should().ContainSingle(run.ToString());
			run.Stdout.Should().Contain("INFO: Disposition=Failed", run.ToString());
			run.Stdout.Should().NotContain("INFO: Initialized", run.ToString());
		}

		[Fact]
		public async Task tolerated_item_failures_are_bounded_ids_then_a_count()
		{
			// ADR-004 §2.2 row 2 / §2.4: 25 items throw, below the threshold of 1000 → Successful, exit 0.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 25, \"millisecondDelayPerItem\": 0, \"throwExceptionInProcessAsync\": true, " +
				"\"execution\": { \"itemFailureCountToStopProcess\": 1000 } }");

			run.ExitCode.Should().Be(0, run.ToString());
			ShouldBePlainLinesOnly(run);
			run.Stdout.Count(l => Regex.IsMatch(l, @"^WARN: item '\d+' failed$")).Should().Be(PlainLineReporter.MaxFailedItemLines, run.ToString());
			run.Stdout.Should().Contain("WARN: 15 more failed item(s) not listed", run.ToString());
			run.Stdout.Should().Contain("INFO: Item results: 25 (omitted) [failed]", run.ToString());
			run.Stdout.Should().Contain("WARN: 1 item message(s) omitted from output", run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task sdk_lines_for_a_10000_item_run_stay_far_below_the_output_budget()
		{
			// ADR-004 §2.4: the platform keeps the first 2000 lines; the SDK's own lines must never approach that.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 10000, \"millisecondDelayPerItem\": 0 }}", timeoutSeconds: 120);

			run.ExitCode.Should().Be(0, run.ToString());
			ShouldBePlainLinesOnly(run);
			run.Stdout.Count.Should().BeLessThan(30, run.ToString());
			run.Stdout.Should().Contain("INFO: Processed 10000/10000 items (10000 successful, 0 failed)", run.ToString());
		}
	}

	/// <summary>DCS-041 unit rules: mode selection, progress throttling, reporter text hygiene.</summary>
	public class Plain_output_rules
	{
		sealed class ManualTime : TimeProvider
		{
			public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero);
			public override DateTimeOffset GetUtcNow() => Now;
		}

		[Theory]
		[InlineData(false, false, false)]
		[InlineData(false, true, true)]
		[InlineData(true, false, true)]
		[InlineData(true, true, true)]
		public void plain_mode_when_forced_or_redirected(bool forcePlain, bool redirected, bool expected) =>
			RunOutputOptions.IsPlain(forcePlain, redirected).Should().Be(expected);

		[Fact]
		public void progress_is_reported_at_most_once_per_interval()
		{
			var time = new ManualTime();
			var throttle = new ProgressThrottle(time, TimeSpan.FromSeconds(30));

			throttle.ShouldReport().Should().BeFalse("nothing is reported before the first interval has passed");

			time.Now += TimeSpan.FromSeconds(29);
			throttle.ShouldReport().Should().BeFalse();

			time.Now += TimeSpan.FromSeconds(1);
			throttle.ShouldReport().Should().BeTrue();
			throttle.ShouldReport().Should().BeFalse("the interval restarts after a report");

			time.Now += TimeSpan.FromMinutes(5);
			throttle.ShouldReport().Should().BeTrue("a long gap yields one line, not a burst");
			throttle.ShouldReport().Should().BeFalse();
		}

		[Fact]
		public void a_process_type_name_containing_a_trigger_is_omitted_from_the_start_line()
		{
			var output = new StringWriter();
			new PlainLineReporter(output).Starting("Acme.ExceptionReportProcessor");

			output.ToString().TrimEnd().Should().Be("INFO: Starting (omitted)");
		}

		[Fact]
		public void reporter_text_is_escaped_onto_one_line()
		{
			var omitted = 0;

			ReporterText.Safe("a\nb", ref omitted).Should().Be("a\\nb");
			ReporterText.Safe("TimeoutException", ref omitted).Should().Be(ReporterText.Omitted);
			omitted.Should().Be(1);
		}
	}
}
