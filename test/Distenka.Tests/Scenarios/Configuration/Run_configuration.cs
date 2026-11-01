using FluentAssertions;
using Distenka.Diagnostics;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Configuration
{
	/// <summary>
	/// DCS-044 process-level: `dotnet &lt;dll&gt; run &lt;config&gt;` with the documents the platform writes (no `process` key),
	/// resolution errors as one ERROR: line and exit 1, and results paths resolved against the configuration's folder.
	/// </summary>
	public class Run_configuration
	{
		static void ShouldFailWithOneErrorLine(ProcessRun run, string expected)
		{
			run.ExitCode.Should().Be(1, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.Stdout.Should().NotContain(l => l.TrimStart().StartsWith("at "), "no stack trace; " + run);
			run.ErrorLines.Should().ContainSingle(run.ToString()).Which.Should().Be(expected);
			ExecutorRules.FinalState(run).Should().Be("Failed", run.ToString());
		}

		[Fact]
		public async Task the_platforms_document_without_process_runs_a_single_process_package()
		{
			var run = await ProcessorProcess.RunDiagnosticsAsync("{ \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0 }");

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stdout.Should().Contain("INFO: Starting Distenka.Diagnostics.DiagnosticProcessor", run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task a_document_without_process_fails_cleanly_for_a_two_process_package()
		{
			var run = await ProcessorProcess.RunConfigAsync(ProcessorProcess.TestsCliDll, "{ \"message\": \"hi\" }");

			ShouldFailWithOneErrorLine(run,
				"ERROR: configuration does not name a process and the package contains 2 processes (Distenka.Tests.Cli.LoggingProcessor, Distenka.Tests.Cli.TestProcessor)");
		}

		[Fact]
		public async Task a_short_process_name_runs_that_process()
		{
			var run = await ProcessorProcess.RunConfigAsync(ProcessorProcess.TestsCliDll,
				"{ \"process\": \"LoggingProcessor\", \"level\": \"Information\", \"message\": \"short name\" }");

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stdout.Should().Contain("INFO: [LoggingProcessor] short name", run.ToString());
		}

		[Fact]
		public async Task an_unknown_process_fails_cleanly_listing_the_available_ones()
		{
			var run = await ProcessorProcess.RunConfigAsync(ProcessorProcess.TestsCliDll, "{ \"process\": \"Nope\" }");

			ShouldFailWithOneErrorLine(run,
				"ERROR: process 'Nope' is not in this package; available: Distenka.Tests.Cli.LoggingProcessor, Distenka.Tests.Cli.TestProcessor");
		}

		[Fact]
		public async Task an_unparseable_document_fails_cleanly()
		{
			var run = await ProcessorProcess.RunDiagnosticsAsync("{ \"numberOfItems\": ");

			run.ExitCode.Should().Be(1, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.ErrorLines.Should().ContainSingle(run.ToString()).Which.Should().StartWith("ERROR: configuration is not valid JSON: ");
		}

		[Fact]
		public async Task a_relative_results_path_is_resolved_against_the_configurations_folder()
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath,
				"{ \"numberOfItems\": 2, \"millisecondDelayPerItem\": 0, \"execution\": { \"resultsToFile\": true, \"resultsFilePath\": \"dcs-044-results.json\" } }");

			var dll = ProcessorProcess.DiagnosticsDll;
			var run = await ProcessorProcess.RunAsync(dll, new[] { "run", configPath }, whileRunning: null, timeoutSeconds: 60);

			run.ExitCode.Should().Be(0, run.ToString());
			File.Exists(Path.Combine(dir, "dcs-044-results.json")).Should().BeTrue("results go next to the configuration");
			File.Exists(Path.Combine(Path.GetDirectoryName(dll), "dcs-044-results.json")).Should().BeFalse("not into the working directory");
		}
	}

	/// <summary>DCS-044: building a `run` host no longer changes the process-wide current directory.</summary>
	[Collection(nameof(Running.InProcessRunCollection))]
	public class Run_working_directory
	{
		[Fact]
		public void building_a_run_host_leaves_the_current_directory_unchanged()
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath, "{ \"numberOfItems\": 1 }");

			var before = Directory.GetCurrentDirectory();

			try
			{
				using var host = ProcessorHost.CreateDefaultBuilder(new[] { "run", configPath, "--silent" }, typeof(DiagnosticProcessor).Assembly).Build();

				Directory.GetCurrentDirectory().Should().Be(before);
			}
			finally
			{
				Directory.SetCurrentDirectory(before);
			}
		}
	}
}
