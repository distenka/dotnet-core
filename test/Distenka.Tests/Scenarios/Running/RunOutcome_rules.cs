using FluentAssertions;
using Distenka.Hosting;
using System;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>DCS-040: unit rules of <see cref="RunOutcome"/> (ADR-004 §2.1, ADR-009 §2.2 line shape).</summary>
	public class RunOutcome_rules
	{
		[Theory]
		[InlineData(Disposition.Successful, 0)]
		[InlineData(Disposition.Failed, 1)]
		[InlineData(Disposition.Cancelled, 1)]
		[InlineData(Disposition.NotComplete, 1)]
		public void exit_code_is_0_only_for_Successful(Disposition disposition, int expected) =>
			RunOutcome.ExitCodeFor(disposition).Should().Be(expected);

		[Theory]
		[InlineData("TimeoutException", true)]
		[InlineData("ERROR: x", true)]
		[InlineData("error: x", true)]
		[InlineData("Error: x", true)]
		[InlineData("timeoutexception", false)]
		[InlineData("error : x", false)]
		[InlineData("Rejected", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void error_triggers_match_the_executor_heuristics(string text, bool expected)
		{
			RunOutcome.ContainsErrorTrigger(text).Should().Be(expected);

			if (text != null)
				ExecutorRules.IsErrorStdoutLine("INFO: " + text).Should().Be(expected, "the SDK check must agree with the executor port");
		}

		[Fact]
		public void error_line_is_one_physical_line()
		{
			var line = RunOutcome.ErrorLine("first\r\nsecond\tthird\u001b[31m");

			line.Should().Be("ERROR: first\\r\\nsecond\\u0009third\\u001B[31m");
			ExecutorRules.ParseLogLevel(line).Should().Be("ERROR");
		}

		[Fact]
		public void error_line_is_capped_at_8192_characters_and_says_how_much_was_cut()
		{
			var line = RunOutcome.ErrorLine(new string('x', 20000));

			line.Length.Should().BeLessOrEqualTo(RunOutcome.MaxLineLength);
			line.Should().StartWith("ERROR: xxx");

			var kept = line.IndexOf('…');
			line.Should().EndWith($"… (+{"ERROR: ".Length + 20000 - kept} chars)");
		}

		[Fact]
		public void unhandled_cancellation_during_host_shutdown_is_reported_as_cancelled()
		{
			RunOutcome.DescribeUnhandled(new OperationCanceledException(), hostStopRequested: true)
				.Should().Be("ERROR: run cancelled (host shutdown requested)");

			RunOutcome.DescribeUnhandled(new InvalidOperationException("boom"), hostStopRequested: false)
				.Should().Be("ERROR: unhandled System.InvalidOperationException: boom");
		}
	}
}
