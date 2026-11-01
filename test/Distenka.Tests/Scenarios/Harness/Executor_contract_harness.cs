using Distenka.Testing;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Harness
{
	/// <summary>
	/// DCS-093: the executor's rules as shipped in Distenka.Processor.Testing (<see cref="Distenka.Testing.ExecutorRules"/>),
	/// applied to recorded output by <see cref="ExecutorContractHarness"/> — verdicts and contract violations.
	/// </summary>
	public class Executor_contract_rules
	{
		const string Successful = "{\"distenkaSummary\":1,\"disposition\":\"Successful\"}";
		const string Failed = "{\"distenkaSummary\":1,\"disposition\":\"Failed\"}";

		/// <summary>Lines in arrival order; a leading '!' marks a stderr line.</summary>
		static IReadOnlyList<CapturedLine> Lines(params string[] lines) =>
			lines.Select(l => l.StartsWith("!", StringComparison.Ordinal) ? new CapturedLine(true, l.Substring(1)) : new CapturedLine(false, l)).ToList();

		[Fact]
		public void the_rules_are_pinned_to_the_executor_commit_they_mirror()
		{
			// AC 3.
			Distenka.Testing.ExecutorRules.SourceCommit.Should().Be("4c3cf2c");
			Distenka.Testing.ExecutorRules.ProgramLastChangedCommit.Should().Be("cdd1a54");
			Distenka.Testing.ExecutorRules.MaxOutputLines.Should().Be(2000);
		}

		[Theory]
		[InlineData("INFO: ok", "INFO")]
		[InlineData("ERROR: x", "ERROR")]
		[InlineData("error: x", "ERROR")]
		[InlineData("x Error: y", "ERROR")]
		[InlineData("WARN: x", "WARNING")]
		[InlineData("warning: x", "WARNING")]
		[InlineData("x Exception y", "ERROR")]
		[InlineData("x exception y", "INFO")]
		[InlineData("Error x", "INFO")]
		[InlineData("WARN: TimeoutException", "WARNING")]
		public void ParseLogLevel_is_the_executors(string line, string expected)
		{
			// AC 3: Program.cs:427-433, verbatim.
			Distenka.Testing.ExecutorRules.ParseLogLevel(line).Should().Be(expected);
		}

		[Theory]
		[InlineData("WARN: TimeoutException", true)]
		[InlineData("WARN: timed out", false)]
		[InlineData("INFO: error : disk", false)]
		[InlineData("INFO: x error: y", true)]
		public void a_stdout_line_with_Exception_sets_hasPluginErrors_whatever_its_level(string line, bool expected)
		{
			// Program.cs:245-260: level == "ERROR" || line.Contains("Exception").
			Distenka.Testing.ExecutorRules.IsErrorStdoutLine(line).Should().Be(expected);
		}

		[Theory]
		[InlineData(0, false, true)]
		[InlineData(0, true, false)]
		[InlineData(1, false, false)]
		[InlineData(-1, false, null)]
		public void pluginSuccess_is_exit_0_without_plugin_errors_and_needs_infra_success(int exitCode, bool hasPluginErrors, bool? expected)
		{
			// AC 3: infraSuccess = exitCode != -1; pluginSuccess = infraSuccess ? (exitCode == 0 && !hasPluginErrors) : null.
			Distenka.Testing.ExecutorRules.PluginSuccess(exitCode, hasPluginErrors).Should().Be(expected);
			Distenka.Testing.ExecutorRules.FinalState(expected).Should().Be(expected == true ? "Completed" : "Failed");
		}

		[Fact]
		public void ExtractJsonObject_counts_raw_braces_even_inside_strings()
		{
			Distenka.Testing.ExecutorRules.ExtractJsonObject("banner\n{\"x\":{}} trailing").Should().Be("{\"x\":{}}");
			Distenka.Testing.ExecutorRules.ExtractJsonObject("{\"a\":\"}\"}").Should().Be("{\"a\":\"}", "the executor's scan ignores JSON strings");
			Distenka.Testing.ExecutorRules.ExtractJsonObject("no json").Should().BeNull();
		}

		[Fact]
		public void a_clean_successful_run_complies()
		{
			var verdict = ExecutorContractHarness.EvaluateRun(0, false, Lines("INFO: Starting P", "INFO: Disposition=Successful", Successful));

			verdict.PluginSuccess.Should().BeTrue(verdict.ToString());
			verdict.FinalState.Should().Be("Completed");
			verdict.SummaryDisposition.Should().Be("Successful");
			verdict.SummaryLine.Should().Be(Successful);
			verdict.Output.Should().Be("INFO: Starting P\nINFO: Disposition=Successful\n" + Successful);
			verdict.Violations.Should().BeEmpty();
		}

		[Fact]
		public void a_stdout_line_containing_Exception_on_a_success_path_is_a_violation()
		{
			// AC 2.
			var verdict = ExecutorContractHarness.EvaluateRun(0, false,
				Lines("INFO: Starting P", "INFO: retried after TimeoutException", "INFO: Disposition=Successful", Successful));

			verdict.ExitCode.Should().Be(0);
			verdict.PluginSuccess.Should().BeFalse("the executor marks the run failed");
			verdict.FinalState.Should().Be("Failed");
			verdict.Violations.Should().ContainSingle(verdict.ToString())
				.Which.Should().Contain("exit 0").And.Contain("INFO: retried after TimeoutException");
		}

		[Fact]
		public void a_clean_failed_run_complies()
		{
			var verdict = ExecutorContractHarness.EvaluateRun(1, false, Lines("INFO: Starting P", "ERROR: InitializeAsync: boom", Failed));

			verdict.PluginSuccess.Should().BeFalse();
			verdict.FinalState.Should().Be("Failed");
			verdict.Violations.Should().BeEmpty(verdict.ToString());
		}

		[Fact]
		public void a_failed_run_without_an_ERROR_line_is_a_violation()
		{
			var verdict = ExecutorContractHarness.EvaluateRun(1, false, Lines("INFO: Starting P", Failed));

			verdict.Violations.Should().ContainSingle().Which.Should().Contain("without an `ERROR:` line");
		}

		[Fact]
		public void stderr_fails_the_run_and_is_a_violation()
		{
			var verdict = ExecutorContractHarness.EvaluateRun(0, false, Lines("INFO: Starting P", "!warning from a library", Successful));

			verdict.HasPluginErrors.Should().BeTrue();
			verdict.PluginSuccess.Should().BeFalse();
			verdict.Stderr.Should().Equal("warning from a library");
			verdict.Violations.Should().ContainSingle().Which.Should().Contain("stderr");
		}

		[Fact]
		public void a_missing_or_contradicting_summary_is_a_violation()
		{
			ExecutorContractHarness.EvaluateRun(0, false, Lines("INFO: Starting P", "INFO: Disposition=Successful"))
				.Violations.Should().ContainSingle().Which.Should().Contain("not the run summary");

			ExecutorContractHarness.EvaluateRun(0, false, Lines("INFO: Starting P", Failed))
				.Violations.Should().ContainSingle().Which.Should().Contain("disagrees");
		}

		[Fact]
		public void an_exit_code_other_than_0_or_1_is_a_violation()
		{
			ExecutorContractHarness.EvaluateRun(134, false, Lines("ERROR: unhandled", Failed))
				.Violations.Should().ContainSingle().Which.Should().Contain("exit code 134");
		}

		[Fact]
		public void a_killed_run_is_an_infrastructure_failure()
		{
			var verdict = ExecutorContractHarness.EvaluateRun(0, true, Lines("INFO: Starting P"));

			verdict.TimedOut.Should().BeTrue();
			verdict.ExitCode.Should().Be(-1);
			verdict.InfraSuccess.Should().BeFalse();
			verdict.PluginSuccess.Should().BeNull();
			verdict.FinalState.Should().Be("Failed");
			verdict.Violations.Should().ContainSingle().Which.Should().Contain("killed");
		}

		[Fact]
		public void output_beyond_2000_lines_is_cut_and_reported()
		{
			var lines = Enumerable.Range(0, 2100).Select(i => $"INFO: line {i}").Append(Successful).ToArray();

			var verdict = ExecutorContractHarness.EvaluateRun(0, false, Lines(lines));

			verdict.Output.Split('\n').Should().HaveCount(2000);
			verdict.Output.Should().NotContain("distenkaSummary", "the platform's output loses the summary");
			verdict.SummaryLine.Should().Be(Successful, "the process still wrote it last");
			verdict.Violations.Should().ContainSingle().Which.Should().Contain("first 2000");
		}

		[Fact]
		public void a_list_that_prints_exactly_one_usable_schema_complies()
		{
			var verdict = ExecutorContractHarness.EvaluateList(0, false, Lines("{\"name\":{\"type\":\"string\",\"required\":true}}"), 16);

			verdict.Accepted.Should().BeTrue();
			verdict.SchemaUsable.Should().BeTrue(verdict.ToString());
			verdict.Schema.Should().Be("{\"name\":{\"type\":\"string\",\"required\":true}}");
			verdict.Violations.Should().BeEmpty();
		}

		[Fact]
		public void a_banner_around_the_schema_is_accepted_by_the_platform_but_a_violation()
		{
			var verdict = ExecutorContractHarness.EvaluateList(0, false, Lines("Distenka v2", "{\"name\":{\"type\":\"string\"}}"), 16);

			verdict.SchemaUsable.Should().BeTrue();
			verdict.Violations.Should().ContainSingle().Which.Should().Contain("besides the schema");
		}

		[Fact]
		public void a_default_configuration_instance_is_an_unusable_schema()
		{
			// What `list` printed before DCS-043: the package uploads, but cannot be invoked.
			var verdict = ExecutorContractHarness.EvaluateList(0, false, Lines("{ \"userId\": 0, \"planId\": 0 }"), 16);

			verdict.Accepted.Should().BeTrue();
			verdict.SchemaUsable.Should().BeFalse();
			verdict.SchemaErrors.Should().Contain(e => e.Contains("'userId'") && e.Contains("rather than by a descriptor object"));
			verdict.Violations.Should().HaveCount(2);
		}

		[Fact]
		public void a_list_that_exits_non_zero_is_rejected_quoting_stderr()
		{
			var verdict = ExecutorContractHarness.EvaluateList(1, false, Lines("{\"ignored\":{}}", "!ERROR: list failed", "!" + new string('x', 3000)), 16);

			verdict.Accepted.Should().BeFalse();
			verdict.Rejection.Should().StartWith("The package exited with code 1 when asked to describe its configuration. ERROR: list failed\n");
			verdict.Rejection.Length.Should().BeLessThan(2100, "stderr is quoted up to 2000 characters");
			verdict.Schema.Should().BeNull("stdout is not consulted on failure");
		}

		[Fact]
		public void a_list_without_a_JSON_object_is_rejected()
		{
			var verdict = ExecutorContractHarness.EvaluateList(0, false, Lines("BroadCast Version: v1"), 16);

			verdict.Accepted.Should().BeFalse();
			verdict.Rejection.Should().Be("The package produced no JSON configuration object on standard output.");
		}

		[Theory]
		[InlineData("[]", "the root is not a JSON object")]
		[InlineData("{\"a\":{\"type\":\"string\",\"pattern\":\"x\"}}", "unknown keyword 'pattern'")]
		[InlineData("{\"a\":{\"type\":\"date\"}}", "missing or unsupported type")]
		[InlineData("{\"a\":{\"type\":\"integer\",\"enum\":[\"x\"]}}", "'enum' only applies to string")]
		[InlineData("{\"a\":{\"type\":\"string\",\"enum\":[]}}", "non-empty array of strings")]
		[InlineData("{\"a\":{\"type\":\"array\"}}", "array without an 'items' descriptor")]
		[InlineData("{\"a\":{\"type\":\"object\"}}", "object without a 'properties' map")]
		[InlineData("{\"a\":{\"type\":\"string\",\"items\":{\"type\":\"string\"}}}", "'items' only applies to array")]
		[InlineData("{\"a\":{\"type\":\"string\",\"minLength\":5,\"maxLength\":2}}", "'minLength' is greater than 'maxLength'")]
		[InlineData("{\"a\":{\"type\":\"string\",\"minLength\":1.5}}", "whole number")]
		[InlineData("{\"a\":{\"type\":\"boolean\",\"minimum\":1}}", "do not apply to boolean")]
		[InlineData("{\"a\":{\"type\":\"string\",\"required\":\"yes\"}}", "'required' is not a boolean")]
		[InlineData("{\"a\":{\"type\":\"string\"},\"a\":{\"type\":\"string\"}}", "duplicate property name")]
		[InlineData("{\" \":{\"type\":\"string\"}}", "blank property name")]
		public void the_validator_refuses_what_the_platform_refuses(string schema, string expected)
		{
			DescriptorSchemaValidator.Validate(schema).Should().Contain(e => e.Contains(expected));
		}

		[Fact]
		public void the_validator_accepts_the_full_dialect()
		{
			const string schema = "{" +
				"\"name\":{\"type\":\"string\",\"required\":true,\"description\":\"d\",\"minLength\":1,\"maxLength\":64,\"default\":\"x\"}," +
				"\"mode\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"],\"nullable\":true}," +
				"\"size\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000}," +
				"\"rate\":{\"type\":\"number\",\"minimum\":0.5}," +
				"\"flag\":{\"type\":\"boolean\"}," +
				"\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"minItems\":0,\"maxItems\":3}," +
				"\"nested\":{\"type\":\"object\",\"properties\":{}}}";

			DescriptorSchemaValidator.Validate(schema).Should().BeEmpty();
			DescriptorSchemaValidator.Validate("{}").Should().BeEmpty("a job that takes no input");
		}

		[Fact]
		public void the_validator_applies_the_schema_depth_limit()
		{
			var schema = "{\"leaf\":{\"type\":\"string\"}}";
			for (var i = 0; i < 10; i++)
				schema = "{\"n\":{\"type\":\"object\",\"properties\":" + schema + "}}";

			DescriptorSchemaValidator.Validate(schema).Should().NotBeEmpty("22 JSON levels exceed the default limit of 16");
			DescriptorSchemaValidator.Validate(schema, maxDepth: 64).Should().BeEmpty();
		}
	}

	/// <summary>
	/// DCS-093, process-level: <see cref="ExecutorContractHarness"/> spawns the built packages the way the executor does.
	/// The Echo sample named in AC 1 does not exist in this repository yet — that check is re-homed to DCS-092; the built
	/// Distenka.Diagnostics and Distenka.Tests.Cli packages stand in for it here.
	/// </summary>
	public class Executor_contract_harness
	{
		const string ProcessKey = "\"process\": \"Distenka.Diagnostics.DiagnosticProcessor\"";

		[Fact]
		public async Task the_Diagnostics_package_publishes_a_usable_schema()
		{
			var verdict = await new ExecutorContractHarness(ProcessorProcess.DiagnosticsDll).ListAsync();

			verdict.ExitCode.Should().Be(0, verdict.ToString());
			verdict.Accepted.Should().BeTrue(verdict.ToString());
			verdict.SchemaUsable.Should().BeTrue(verdict.ToString());
			verdict.Violations.Should().BeEmpty(verdict.ToString());
		}

		[Fact]
		public async Task a_two_process_package_publishes_a_usable_schema()
		{
			var verdict = await new ExecutorContractHarness(ProcessorProcess.TestsCliDll).ListAsync();

			verdict.SchemaUsable.Should().BeTrue(verdict.ToString());
			verdict.Violations.Should().BeEmpty(verdict.ToString());
		}

		[Fact]
		public async Task a_successful_run_is_Completed_with_no_violations()
		{
			// AC 1 (stand-in for the Echo sample): PluginSuccess = true.
			var verdict = await new ExecutorContractHarness(ProcessorProcess.DiagnosticsDll)
				.RunAsync($"{{ {ProcessKey}, \"numberOfItems\": 5, \"millisecondDelayPerItem\": 0 }}");

			verdict.ExitCode.Should().Be(0, verdict.ToString());
			verdict.PluginSuccess.Should().BeTrue(verdict.ToString());
			verdict.FinalState.Should().Be("Completed");
			verdict.SummaryDisposition.Should().Be("Successful");
			verdict.Violations.Should().BeEmpty(verdict.ToString());
			verdict.Output.Split('\n').Last().Should().Be(verdict.SummaryLine);
		}

		[Fact]
		public async Task a_failing_run_is_Failed_and_still_honours_the_contract()
		{
			// AC 1 (stand-in for Echo with fail=true): PluginSuccess = false.
			var verdict = await new ExecutorContractHarness(ProcessorProcess.DiagnosticsDll)
				.RunAsync($"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInInitializeAsync\": true }}");

			verdict.ExitCode.Should().Be(1, verdict.ToString());
			verdict.PluginSuccess.Should().BeFalse(verdict.ToString());
			verdict.FinalState.Should().Be("Failed");
			verdict.SummaryDisposition.Should().Be("Failed");
			verdict.Violations.Should().BeEmpty(verdict.ToString());
		}

		[Fact]
		public async Task a_LogError_on_a_successful_run_is_flagged()
		{
			// AC 2, process-level: the disposition is Successful and the exit code 0, but the ERROR: line the author logged
			// makes the platform mark the run Failed (ADR-009 §2.4 trade-off 3) — the harness says so before upload.
			var verdict = await new ExecutorContractHarness(ProcessorProcess.TestsCliDll)
				.RunAsync("{ \"process\": \"LoggingProcessor\", \"level\": \"Error\", \"message\": \"sync failed\" }");

			verdict.ExitCode.Should().Be(0, verdict.ToString());
			verdict.SummaryDisposition.Should().Be("Successful");
			verdict.PluginSuccess.Should().BeFalse(verdict.ToString());
			verdict.FinalState.Should().Be("Failed");
			verdict.Violations.Should().ContainSingle(verdict.ToString())
				.Which.Should().Contain("exit 0").And.Contain("ERROR: [LoggingProcessor] sync failed");
		}

		[Fact]
		public async Task a_missing_DLL_is_reported_before_spawning()
		{
			var harness = new ExecutorContractHarness(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.dll"));

			await harness.Awaiting(h => h.ListAsync()).Should().ThrowAsync<FileNotFoundException>();
		}
	}
}
