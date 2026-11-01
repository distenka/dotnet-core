using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>
	/// DCS-027 AC 2 (AD-9): a processor that calls the platform reads its account key from an owner-bound configuration
	/// property (ACC-37) and builds an <c>AccountKeyCredential</c> from it; nothing the host writes for the run — stdout, the
	/// ERROR line, stderr, the results file — contains the key. Process-level: spawns <c>Distenka.Tests.KeyCli</c> the way the
	/// executor does. DCS-046 AC 1: the run summary (the last stdout line) is covered by the same checks, and a processor
	/// that carelessly copies the key into its item category, exception message and FinalizeAsync output still gets no
	/// key onto stdout — the SDK redacts what it echoes.
	/// </summary>
	public class Owner_bound_account_key
	{
		/// <summary>A fake key; lower-case on purpose (the SDK canonicalises to upper case), searched case-insensitively.</summary>
		const string Hex = "0123456789abcdef0123456789abcdef0123456789abcdef";
		const string Key = "DK-" + Hex;

		/// <summary>Any DK-/DKI- shaped value long enough to be a key (the public 8-digit prefix does not match).</summary>
		static readonly Regex KeyShape = new Regex("DKI?-[0-9A-Fa-f]{32,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		static async Task<(ProcessRun Run, string Results)> RunAsync(bool fail, bool echoKey = false)
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var resultsPath = Path.Combine(dir, "results.json");

			// The owner binding arrives merged into the run configuration document, like any other property.
			var config = $"{{ \"distenkaApiKey\": \"{Key}\", \"fail\": {(fail ? "true" : "false")}, \"echoKey\": {(echoKey ? "true" : "false")}, " +
				$"\"execution\": {{ \"resultsToFile\": true, \"resultsFilePath\": {Newtonsoft.Json.JsonConvert.ToString(resultsPath)} }} }}";

			var run = await ProcessorProcess.RunConfigAsync(ProcessorProcess.KeyCliDll, config);
			var results = File.Exists(resultsPath) ? File.ReadAllText(resultsPath) : null;
			return (run, results);
		}

		static void NoKeyOnTheConsole(ProcessRun run)
		{
			run.Summary.Should().NotBeNull("the run summary is the last stdout line (DCS-046); " + run);

			foreach (var line in run.Stdout.Concat(run.Stderr))
			{
				line.Should().NotContainEquivalentOf(Hex, run.ToString());
				KeyShape.IsMatch(line).Should().BeFalse(line);
			}
		}

		static void NoKeyAnywhere(ProcessRun run, string results)
		{
			NoKeyOnTheConsole(run);

			results.Should().NotBeNull("the run writes its results file; " + run);
			results.Should().NotContainEquivalentOf(Hex);
			KeyShape.IsMatch(results).Should().BeFalse(results);
		}

		[Fact]
		public async Task a_successful_run_never_writes_the_owner_bound_key()
		{
			var (run, results) = await RunAsync(fail: false);

			run.ExitCode.Should().Be(0, run.ToString());
			run.Stdout.Should().Contain("INFO: [PlatformCallerProcessor] Platform client ready with DK-01234567…",
				"the processor built its credential from the bound key; only the public prefix is shown. " + run);
			NoKeyAnywhere(run, results);
		}

		[Fact]
		public async Task a_failed_run_never_writes_the_owner_bound_key()
		{
			var (run, results) = await RunAsync(fail: true);

			run.ExitCode.Should().NotBe(0, run.ToString());
			run.Stdout.Should().Contain(l => l.StartsWith("ERROR:"), run.ToString());
			NoKeyAnywhere(run, results);
		}

		/// <summary>What the SDK shows instead of the key: the marker and the 8-digit public prefix.</summary>
		const string Shown = "DK-01234567…";

		[Fact]
		public async Task a_key_the_processor_echoes_is_redacted_from_the_reporter_lines_and_the_summary()
		{
			// DCS-046 AC 1: the key is the item category and inside the FinalizeAsync output.
			var (run, _) = await RunAsync(fail: false, echoKey: true);

			run.ExitCode.Should().Be(0, run.ToString());
			NoKeyOnTheConsole(run);
			run.Stdout.Should().Contain($"INFO: Item results: 1 {Shown}", "the reporter echoes the category, redacted; " + run);

			var summary = run.Summary;
			summary.Value<string>("disposition").Should().Be("Successful");
			summary["categories"].Should().ContainSingle().Which.Value<string>("category").Should().Be(Shown);
			summary["output"].Value<string>("submittedWith").Should().Be(Shown);
		}

		[Fact]
		public async Task a_key_in_a_failure_message_is_redacted_from_the_ERROR_line_and_the_summary()
		{
			// The results file keeps the processor's own detail unredacted (it is the processor's file, ADR-004 §2.3 rule 3);
			// only what reaches the console is checked here.
			var (run, _) = await RunAsync(fail: true, echoKey: true);

			run.ExitCode.Should().Be(1, run.ToString());
			NoKeyOnTheConsole(run);
			run.ErrorLines.Should().ContainSingle(run.ToString()).Which.Should().Contain($"Submitting the child run failed with {Shown}.");

			var summary = run.Summary;
			summary.Value<string>("disposition").Should().Be("Failed");
			summary["failures"].Should().ContainSingle().Which.Value<string>("message").Should().Be($"Submitting the child run failed with {Shown}.");
			summary["output"].Value<string>("submittedWith").Should().Be(Shown);
		}
	}
}
