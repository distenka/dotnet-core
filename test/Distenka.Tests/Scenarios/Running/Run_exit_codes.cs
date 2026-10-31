using FluentAssertions;
using Distenka.Diagnostics;
using Distenka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>
	/// DCS-040 / ADR-004 §2.1–2.2: `run` exits 0 only for a Successful disposition, 1 otherwise, with one ERROR: line on
	/// stdout and nothing on stderr; the executor's verdict (ported in <see cref="ExecutorRules"/>) is Completed only for
	/// the successful rows. Process-level: spawns the built Distenka.Diagnostics the way the executor does.
	/// </summary>
	public class Run_exit_codes
	{
		const string ProcessKey = "\"process\": \"Distenka.Diagnostics.DiagnosticProcessor\"";

		static void ShouldBeCleanSuccess(ProcessRun run)
		{
			run.ExitCode.Should().Be(0, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.Stdout.Where(ExecutorRules.IsErrorStdoutLine).Should().BeEmpty(run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		static void ShouldBeFailure(ProcessRun run, string errorLinePrefix)
		{
			run.ExitCode.Should().Be(1, run.ToString());
			run.Stderr.Should().BeEmpty(run.ToString());
			run.ErrorLines.Should().ContainSingle(run.ToString())
				.Which.Should().StartWith(errorLinePrefix, run.ToString());
			ExecutorRules.FinalState(run).Should().Be("Failed", run.ToString());
		}

		[Fact]
		public async Task successful_run_exits_0_with_a_clean_stdout()
		{
			// ADR-004 §2.2 row 1
			var run = await ProcessorProcess.RunDiagnosticsAsync($"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0 }}");

			ShouldBeCleanSuccess(run);
			run.Stdout.Should().Contain(l => l.Contains("Processor SUCCESSFUL"), run.ToString());
		}

		[Fact]
		public async Task tolerated_item_exceptions_exit_0_without_exception_text_on_stdout()
		{
			// ADR-004 §2.2 row 2: every item throws DiagnosticProcessException, but 3 failures stay below the threshold of 10,
			// so the disposition is Successful. The exception type (also the item category) must not reach stdout.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInProcessAsync\": true, " +
				"\"execution\": { \"itemFailureCountToStopProcess\": 10 } }");

			ShouldBeCleanSuccess(run);
			run.Stdout.Should().NotContain(l => l.Contains("DiagnosticProcessException") || l.Contains("   at "), run.ToString());
			run.Stdout.Should().Contain(l => l.Contains("WARN: item '0' failed"), run.ToString());
			run.Stdout.Should().Contain(l => l.StartsWith("WARN: ") && l.Contains("omitted from output"), run.ToString());
		}

		[Fact]
		public async Task exception_in_ProcessAsync_reaching_the_threshold_exits_1()
		{
			// ADR-004 §2.2 row 3 (default ItemFailureCountToStopProcess = 1)
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInProcessAsync\": true }}");

			ShouldBeFailure(run, "ERROR: item failure threshold reached");
			run.ErrorLines.Single().Should().Contain("Distenka.Diagnostics.DiagnosticProcessException");
		}

		[Fact]
		public async Task Result_Failure_reaching_the_threshold_exits_1()
		{
			// ADR-004 §2.2 row 3 without any exception text: before DCS-040 this run was Completed on the platform.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"millisecondDelayPerItem\": 0, " +
				"\"categories\": [ { \"name\": \"Rejected\", \"isSuccessful\": false, \"count\": 1 } ], " +
				"\"execution\": { \"itemFailureCountToStopProcess\": 1 } }");

			ShouldBeFailure(run, "ERROR: item failure threshold reached: 1 failed item(s), ItemFailureCountToStopProcess=1");
		}

		[Fact]
		public async Task exception_in_InitializeAsync_exits_1()
		{
			// ADR-004 §2.2 row 4
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInInitializeAsync\": true }}");

			ShouldBeFailure(run, "ERROR: InitializeAsync: Distenka.Diagnostics.DiagnosticProcessException: ");
		}

		[Fact]
		public async Task SIGTERM_cancels_the_run_and_exits_1_on_linux()
		{
			// ADR-004 §2.2 row 6, local SIGTERM semantics only. Whether SIGTERM reaches the plugin under the platform is OQ-8
			// (DCS-047). Windows has no SIGTERM to send; Run_cancellation covers host shutdown there.
			if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
				return;

			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 1000, \"millisecondDelayPerItem\": 50 }}",
				whileRunning: p =>
				{
					Thread.Sleep(TimeSpan.FromSeconds(3));
					using var kill = System.Diagnostics.Process.Start(new ProcessStartInfo("kill", $"-TERM {p.Id}") { UseShellExecute = false });
					kill.WaitForExit();
				});

			ShouldBeFailure(run, "ERROR: run cancelled (host shutdown requested)");
		}
	}

	[CollectionDefinition(nameof(InProcessRunCollection), DisableParallelization = true)]
	public class InProcessRunCollection { }

	/// <summary>
	/// DCS-040, in-process (all OSes): host shutdown during a run → Cancelled → <see cref="Environment.ExitCode"/> 1 and one
	/// ERROR: line. Runs alone because it redirects <see cref="Console.Out"/> and `run` changes the current directory.
	/// </summary>
	[Collection(nameof(InProcessRunCollection))]
	public class Run_cancellation
	{
		[Fact]
		public async Task host_shutdown_during_a_run_sets_exit_code_1_and_writes_one_ERROR_line()
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath, "{ \"process\": \"Distenka.Diagnostics.DiagnosticProcessor\", \"numberOfItems\": 1000, \"millisecondDelayPerItem\": 20 }");

			var originalOut = Console.Out;
			var originalCwd = Directory.GetCurrentDirectory();
			var output = new StringWriter();

			try
			{
				Console.SetOut(output);

				using var host = ProcessorHost.CreateDefaultBuilder(new[] { "run", configPath, "--silent" }, typeof(DiagnosticProcessor).Assembly).Build();

				var running = host.RunAsync();
				await Task.Delay(TimeSpan.FromSeconds(1));
				host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
				await running;

				Environment.ExitCode.Should().Be(1, output.ToString());
				output.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
					.Where(l => l.StartsWith("ERROR:", StringComparison.Ordinal))
					.Should().ContainSingle(output.ToString())
					.Which.Should().Be("ERROR: run cancelled (host shutdown requested)");
			}
			finally
			{
				Console.SetOut(originalOut);
				Directory.SetCurrentDirectory(originalCwd);
				Environment.ExitCode = 0;
			}
		}

		[Fact]
		public async Task successful_in_process_run_sets_exit_code_0()
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath, "{ \"process\": \"Distenka.Diagnostics.DiagnosticProcessor\", \"numberOfItems\": 2, \"millisecondDelayPerItem\": 0 }");

			var originalOut = Console.Out;
			var originalCwd = Directory.GetCurrentDirectory();
			var output = new StringWriter();

			try
			{
				Console.SetOut(output);
				Environment.ExitCode = 42;

				using var host = ProcessorHost.CreateDefaultBuilder(new[] { "run", configPath, "--silent" }, typeof(DiagnosticProcessor).Assembly).Build();
				await host.RunAsync();

				Environment.ExitCode.Should().Be(0, output.ToString());
				output.ToString().Should().NotContain("ERROR:");
			}
			finally
			{
				Console.SetOut(originalOut);
				Directory.SetCurrentDirectory(originalCwd);
				Environment.ExitCode = 0;
			}
		}
	}
}
