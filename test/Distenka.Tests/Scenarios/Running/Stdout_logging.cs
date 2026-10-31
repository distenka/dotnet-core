using FluentAssertions;
using Distenka.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>
	/// DCS-042 / ADR-009: processor <c>ILogger</c> output reaches stdout as one executor-compatible line per event, and
	/// the executor's verdict follows the ADR-009 rules. Process-level: spawns <c>Distenka.Tests.Cli</c>'s
	/// <c>LoggingProcessor</c> with stdout redirected (plain mode) and a near-empty environment.
	/// </summary>
	public class Stdout_logging
	{
		const string ProcessKey = "\"process\": \"Distenka.Tests.Cli.LoggingProcessor\"";

		static Task<ProcessRun> Log(string level, string message, string exception = null, int messageLength = 0, IDictionary<string, string> environment = null) =>
			ProcessorProcess.RunConfigAsync(ProcessorProcess.TestsCliDll,
				$"{{ {ProcessKey}, \"level\": \"{level}\", \"message\": {Newtonsoft.Json.JsonConvert.ToString(message)}, " +
				$"\"messageLength\": {messageLength}, \"exception\": {(exception is null ? "null" : $"\"{exception}\"")} }}",
				environment);

		static IEnumerable<string> Logged(ProcessRun run) => run.Stdout.Where(l => l.Contains("[LoggingProcessor]"));

		[Fact]
		public async Task a_warning_appears_as_a_WARN_line_with_the_short_category()
		{
			var run = await Log("Warning", "x");

			run.ExitCode.Should().Be(0, run.ToString());
			Logged(run).Should().ContainSingle(run.ToString()).Which.Should().Be("WARN: [LoggingProcessor] x");
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task a_warning_with_a_TimeoutException_is_neutralised_and_the_run_stays_Completed()
		{
			var run = await Log("Warning", "Retrying", exception: "timeout");

			Logged(run).Should().ContainSingle(run.ToString())
				.Which.Should().Be("WARN: [LoggingProcessor] Retrying | System.Timeoutexception: timed out");
			run.ExitCode.Should().Be(0, run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task an_information_line_with_error_colon_is_neutralised_and_the_run_stays_Completed()
		{
			var run = await Log("Information", "No error: 0");

			Logged(run).Should().ContainSingle(run.ToString()).Which.Should().Be("INFO: [LoggingProcessor] No error : 0");
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Theory]
		[InlineData("Warning", "Retrying", "timeout")]
		[InlineData("Information", "No error: 0", null)]
		public async Task with_neutralisation_turned_off_the_same_lines_fail_the_run(string level, string message, string exception)
		{
			var run = await Log(level, message, exception,
				environment: new Dictionary<string, string> { ["Distenka__Logging__NeutraliseHeuristicTriggers"] = "false" });

			run.ExitCode.Should().Be(0, "the execution itself succeeded; " + run);
			ExecutorRules.FinalState(run).Should().Be("Failed", "the executor's heuristics see the raw text; " + run);
		}

		[Fact]
		public async Task LogError_with_an_exception_is_one_ERROR_line_with_the_stack_escaped_and_fails_a_Successful_run()
		{
			var run = await Log("Error", "Failed to sync", exception: "thrown");

			run.ExitCode.Should().Be(0, "LogError does not change the disposition; " + run);
			var line = Logged(run).Should().ContainSingle(run.ToString()).Which;
			line.Should().StartWith("ERROR: [LoggingProcessor] Failed to sync | System.InvalidOperationException: boom | ");
			line.Should().Contain("LoggingProcessor.Thrown", "the stack trace is on the same line");
			line.Should().NotContain("\n").And.NotContain("\r");
			ExecutorRules.FinalState(run).Should().Be("Failed", "ERROR: lines fail the run on the platform (ADR-009 §2.4 trade-off 3); " + run);
		}

		[Fact]
		public async Task a_message_with_a_newline_and_ESC_stays_one_line_without_raw_control_characters()
		{
			var run = await Log("Information", "first\nsecond\u001b[31mred");

			Logged(run).Should().ContainSingle(run.ToString())
				.Which.Should().Be("INFO: [LoggingProcessor] first\\nsecond\\u001B[31mred");
			run.Stdout.Should().NotContain(l => l.Any(c => c < 0x20), run.ToString());
		}

		[Fact]
		public async Task a_10_KB_message_is_capped_at_8192_characters()
		{
			var run = await Log("Information", "", messageLength: 10 * 1024);

			var line = Logged(run).Should().ContainSingle(run.ToString()).Which;
			line.Length.Should().BeLessOrEqualTo(RunOutcome.MaxLineLength);
			line.Should().MatchRegex(@"… \(\+\d+ chars\)$");
		}

		[Fact]
		public async Task non_ASCII_text_reaches_the_executor_as_UTF8()
		{
			// Found by the first local run: on Windows a redirected stdout used the OEM code page and the "…" of a capped
			// line arrived as ".". The executor reads UTF-8; plain mode now writes UTF-8 on every OS.
			var run = await Log("Information", "café ✓ 日本");

			Logged(run).Should().ContainSingle(run.ToString()).Which.Should().Be("INFO: [LoggingProcessor] café ✓ 日本");
		}

		[Fact]
		public async Task no_host_lifetime_lines_appear()
		{
			var run = await Log("Information", "hello");

			run.Stdout.Should().NotContain(l => l.Contains("Application started") || l.Contains("Hosting environment") || l.Contains("Content root path") || l.Contains("Application is shutting down"), run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
		}

		[Fact]
		public async Task log_levels_respect_the_Logging_configuration_section()
		{
			var env = new Dictionary<string, string> { ["Logging__LogLevel__Default"] = "Warning" };

			var info = await Log("Information", "quiet", environment: env);
			Logged(info).Should().BeEmpty(info.ToString());

			var debugEnabled = await Log("Debug", "verbose", environment: new Dictionary<string, string> { ["Logging__LogLevel__Default"] = "Debug" });
			Logged(debugEnabled).Should().ContainSingle(debugEnabled.ToString()).Which.Should().Be("DEBUG: [LoggingProcessor] verbose");

			var debugDefault = await Log("Debug", "verbose");
			Logged(debugDefault).Should().BeEmpty("the default minimum level is Information; " + debugDefault);
		}
	}

	/// <summary>DCS-042 unit rules of the ADR-009 §2.2 line format and §2.4 neutralisation.</summary>
	public class StdoutLine_format
	{
		[Theory]
		[InlineData(LogLevel.Trace, "DEBUG:")]
		[InlineData(LogLevel.Debug, "DEBUG:")]
		[InlineData(LogLevel.Information, "INFO:")]
		[InlineData(LogLevel.Warning, "WARN:")]
		[InlineData(LogLevel.Error, "ERROR:")]
		[InlineData(LogLevel.Critical, "ERROR:")]
		public void each_level_maps_to_an_executor_prefix(LogLevel level, string prefix)
		{
			StdoutLineFormatter.Prefix(level).Should().Be(prefix);
			ExecutorRules.ParseLogLevel(StdoutLineFormatter.Format(level, "C", "m", null, neutralise: true))
				.Should().Be(prefix == "WARN:" ? "WARNING" : prefix == "DEBUG:" ? "INFO" : prefix.TrimEnd(':'));
		}

		[Theory]
		[InlineData("Acme.Jobs.SyncProcessor", "SyncProcessor")]
		[InlineData("Acme.Jobs.Repo`1", "Repo")]
		[InlineData("Distenka.Processing.ExecutionBase<Acme.Config, System.String>", "ExecutionBase")]
		[InlineData("Plain", "Plain")]
		[InlineData("", "")]
		public void category_is_shortened_to_its_last_segment(string category, string expected) =>
			StdoutLineFormatter.ShortCategory(category).Should().Be(expected);

		[Fact]
		public void exception_segments_cover_inner_exceptions_and_the_stack_is_only_on_ERROR_lines()
		{
			Exception ex;
			try { throw new InvalidOperationException("outer", new ArgumentException("inner")); }
			catch (Exception e) { ex = e; }

			var warn = StdoutLineFormatter.Format(LogLevel.Warning, "C", "m", ex, neutralise: false);
			warn.Should().Be("WARN: [C] m | System.InvalidOperationException: outer | System.ArgumentException: inner");

			var error = StdoutLineFormatter.Format(LogLevel.Error, "C", "m", ex, neutralise: true);
			error.Should().StartWith("ERROR: [C] m | System.InvalidOperationException: outer | System.ArgumentException: inner | ");
			error.Should().Contain(nameof(exception_segments_cover_inner_exceptions_and_the_stack_is_only_on_ERROR_lines));
			error.Should().NotContainAny("\r", "\n");
		}

		[Theory]
		[InlineData("TimeoutException", "Timeoutexception")]
		[InlineData("No error: 0", "No error : 0")]
		[InlineData("ERROR: disk", "ERROR : disk")]
		[InlineData("Error: x and error: y", "Error : x and error : y")]
		[InlineData("errors are fine", "errors are fine")]
		[InlineData("exception", "exception")]
		public void neutralisation_rewrites_only_the_triggers(string text, string expected) =>
			StdoutLineFormatter.Neutralise(text).Should().Be(expected);

		[Theory]
		[InlineData(LogLevel.Debug)]
		[InlineData(LogLevel.Information)]
		[InlineData(LogLevel.Warning)]
		public void neutralised_non_error_lines_never_trip_the_executor(LogLevel level)
		{
			var line = StdoutLineFormatter.Format(level, "C", "TimeoutException error: ERROR: Exception", null, neutralise: true);

			ExecutorRules.IsErrorStdoutLine(line).Should().BeFalse(line);
		}

		[Fact]
		public void ERROR_lines_are_never_neutralised()
		{
			StdoutLineFormatter.Format(LogLevel.Error, "C", "TimeoutException error: x", null, neutralise: true)
				.Should().Be("ERROR: [C] TimeoutException error: x");
		}

		[Fact]
		public void the_provider_writes_through_its_writer_and_neutralises_only_in_plain_mode()
		{
			var lines = new List<string>();
			var plain = new StdoutLineLoggerProvider(new StdoutLineLoggerOptions { Plain = true }, lines.Add);
			plain.CreateLogger("Acme.Worker").LogWarning("No error: {Count}", 0);

			var interactive = new StdoutLineLoggerProvider(new StdoutLineLoggerOptions { Plain = false }, lines.Add);
			interactive.CreateLogger("Acme.Worker").LogWarning("No error: {Count}", 0);

			var optedOut = new StdoutLineLoggerProvider(new StdoutLineLoggerOptions { Plain = true, NeutraliseHeuristicTriggers = false }, lines.Add);
			optedOut.CreateLogger("Acme.Worker").LogWarning("No error: {Count}", 0);

			lines.Should().Equal("WARN: [Worker] No error : 0", "WARN: [Worker] No error: 0", "WARN: [Worker] No error: 0");
		}
	}
}
