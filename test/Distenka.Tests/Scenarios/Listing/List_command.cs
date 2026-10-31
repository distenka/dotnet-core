using FluentAssertions;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Listing
{
	/// <summary>
	/// DCS-043 process-level: `dotnet &lt;dll&gt; list` exactly as the executor runs it at package validation (no flags,
	/// stdout/stderr redirected, near-empty environment), checked with the executor's ExtractJsonObject and a port of the
	/// platform's descriptor rules.
	/// </summary>
	public class List_command
	{
		const string DiagnosticsGolden =
			"{\"process\":{\"type\":\"string\",\"required\":false,\"enum\":[\"Distenka.Diagnostics.DiagnosticProcessor\"],\"description\":\"Process to run. One of: Distenka.Diagnostics.DiagnosticProcessor.\"},"
			+ "\"numberOfItems\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647,\"description\":\"The number of items to process.\"},"
			+ "\"categories\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"isSuccessful\":{\"type\":\"boolean\"},\"count\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}}}},"
			+ "\"millisecondDelayPerItem\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647},"
			+ "\"canCountItems\":{\"type\":\"boolean\"},"
			+ "\"waitForSignalInInitializeAsync\":{\"type\":\"boolean\"},"
			+ "\"waitForSignalInProcessAsync\":{\"type\":\"boolean\"},"
			+ "\"waitForSignalInFinalizeAsync\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInInitializeAsync\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInGetItemsAsync\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInGetEnumerator\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInEnumeratorMoveNext\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInEnumeratorCurrent\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInGetItemIdAsync\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInProcessAsync\":{\"type\":\"boolean\"},"
			+ "\"throwExceptionInFinalizeAsync\":{\"type\":\"boolean\"},"
			+ "\"messageToLogInProcessAsync\":{\"type\":\"string\"}}";

		const string CliGolden =
			"{\"process\":{\"type\":\"string\",\"required\":true,\"enum\":[\"Distenka.Tests.Cli.LoggingProcessor\",\"Distenka.Tests.Cli.TestProcessor\"],\"description\":\"Process to run. One of: Distenka.Tests.Cli.LoggingProcessor, Distenka.Tests.Cli.TestProcessor.\"},"
			+ "\"level\":{\"type\":\"string\"},\"message\":{\"type\":\"string\"},\"messageLength\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647},\"exception\":{\"type\":\"string\",\"nullable\":true}}";

		static Task<ProcessRun> List(string dll, params string[] flags) =>
			ProcessorProcess.RunAsync(dll, new[] { "list" }.Concat(flags).ToArray(), whileRunning: null, timeoutSeconds: 60);

		[Fact]
		public async Task diagnostics_list_prints_exactly_the_golden_descriptor()
		{
			var run = await List(ProcessorProcess.DiagnosticsDll);

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.Stdout.Should().ContainSingle("nothing else is written to stdout; " + run).Which.Should().Be(DiagnosticsGolden);
		}

		[Fact]
		public async Task the_executor_extracts_and_the_platform_accepts_the_diagnostics_descriptor()
		{
			var run = await List(ProcessorProcess.DiagnosticsDll);
			var stdout = string.Join("\n", run.Stdout);

			var extracted = DescriptorDialect.ExtractJsonObject(stdout);
			extracted.Should().Be(stdout);
			DescriptorDialect.Validate(extracted).Should().BeEmpty();
		}

		[Fact]
		public async Task a_two_process_package_publishes_a_required_process_and_the_union()
		{
			var run = await List(ProcessorProcess.TestsCliDll);

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.Stdout.Should().ContainSingle(run.ToString()).Which.Should().Be(CliGolden);
			DescriptorDialect.Validate(run.Stdout.Single()).Should().BeEmpty();
		}

		[Fact]
		public async Task list_is_byte_identical_across_runs()
		{
			var first = await List(ProcessorProcess.DiagnosticsDll);
			var second = await List(ProcessorProcess.DiagnosticsDll);

			second.Stdout.Should().Equal(first.Stdout);
		}

		[Fact]
		public async Task list_verbose_is_a_readable_listing()
		{
			var run = await List(ProcessorProcess.DiagnosticsDll, "--verbose");

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stdout.Should().Contain(l => l.StartsWith("Process:") && l.Contains("Distenka.Diagnostics.DiagnosticProcessor"), run.ToString());
			run.Stdout.Should().Contain("Input schema (what `list` prints for the platform):", run.ToString());
		}

		[Fact]
		public async Task list_legacy_json_prints_the_1x_listing()
		{
			var run = await List(ProcessorProcess.DiagnosticsDll, "--legacy-json");

			run.ExitCode.Should().Be(0, run.ToString());
			var json = JObject.Parse(string.Join("\n", run.Stdout));
			// Same camelCase serializer (ConfigWriter.Serializer) as 1.x: the keys were always clientVersion / processes.
			json["clientVersion"].Should().NotBeNull();
			json["processes"].Should().ContainSingle(run.ToString());
			((string)json["processes"][0]["processType"]).Should().Be("Distenka.Diagnostics.DiagnosticProcessor");
		}

		[Fact]
		public async Task list_json_is_withdrawn()
		{
			var run = await List(ProcessorProcess.DiagnosticsDll, "--json");

			run.ExitCode.Should().Be(1, run.ToString());
			run.Stdout.Should().BeEmpty(run.ToString());
			run.Stderr.Should().ContainSingle(run.ToString()).Which.Should().StartWith("ERROR: list --json was withdrawn");
		}

		[Fact]
		public async Task get_writes_a_runnable_example_config_without_nulls()
		{
			var run = await ProcessorProcess.RunAsync(ProcessorProcess.DiagnosticsDll,
				new[] { "get", "Distenka.Diagnostics.DiagnosticProcessor" }, whileRunning: null, timeoutSeconds: 60);

			run.ExitCode.Should().Be(0, run.ToString());
			var example = JObject.Parse(string.Join("\n", run.Stdout));
			((string)example["process"]).Should().Be("Distenka.Diagnostics.DiagnosticProcessor");
			example.Properties().Should().NotContain(p => p.Value.Type == JTokenType.Null, "nulls are refused by the non-nullable descriptors");
			example["execution"].Should().BeNull();
		}
	}
}
