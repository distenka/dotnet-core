using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Tests
{
	/// <summary>
	/// A port of the platform executor's run-path rules, used to assert agreement with ADR-004.
	/// Mirrors <c>D:\Product\Distenka\Distenka.Core.Executor\Program.cs</c> @ <c>4c3cf2c</c> (file last changed <c>cdd1a54</c>):
	/// stdout handler <c>:245-260</c>, stderr handler <c>:262-276</c>, verdict <c>:326-328</c> / <c>:357-359</c>,
	/// <c>ParseLogLevel</c> <c>:427-433</c>. Re-verify whenever that file changes. DCS-093 replaces this minimal port with the
	/// reusable <c>ExecutorContractHarness</c>.
	/// </summary>
	static class ExecutorRules
	{
		/// <summary><c>ParseLogLevel</c>, <c>Program.cs:427-433</c>.</summary>
		public static string ParseLogLevel(string line)
		{
			if (Regex.IsMatch(line, @"ERROR:", RegexOptions.IgnoreCase)) return "ERROR";
			if (Regex.IsMatch(line, @"(WARNING|WARN):", RegexOptions.IgnoreCase)) return "WARNING";
			if (line.Contains("Exception") || line.Contains("error:")) return "ERROR";
			return "INFO";
		}

		/// <summary>Whether a stdout line sets <c>hasPluginErrors</c> (<c>Program.cs:245-260</c>).</summary>
		public static bool IsErrorStdoutLine(string line) =>
			ParseLogLevel(line) == "ERROR" || line.Contains("Exception");

		/// <summary>The platform's final run state for a finished plugin process.</summary>
		public static string FinalState(ProcessRun run)
		{
			// Any stderr line sets hasPluginErrors (:262-276).
			var hasPluginErrors = run.Stderr.Count > 0 || run.Stdout.Any(IsErrorStdoutLine);

			var infraSuccess = run.ExitCode != -1;
			bool? pluginSuccess = infraSuccess ? (run.ExitCode == 0 && !hasPluginErrors) : (bool?)null;

			return infraSuccess ? (pluginSuccess == true ? "Completed" : "Failed") : "Failed";
		}
	}

	/// <summary>The captured result of a spawned processor process.</summary>
	sealed class ProcessRun
	{
		public int ExitCode { get; init; }
		public IReadOnlyList<string> Stdout { get; init; }
		public IReadOnlyList<string> Stderr { get; init; }

		public IEnumerable<string> ErrorLines => Stdout.Where(l => l.StartsWith("ERROR:", StringComparison.Ordinal));

		public override string ToString() =>
			$"exit {ExitCode}{Environment.NewLine}--- stdout ---{Environment.NewLine}{string.Join(Environment.NewLine, Stdout)}" +
			$"{Environment.NewLine}--- stderr ---{Environment.NewLine}{string.Join(Environment.NewLine, Stderr)}";
	}

	/// <summary>
	/// Spawns <c>dotnet &lt;dll&gt; run &lt;config&gt;</c> the way the executor does (<c>Program.cs:220-243</c>): stdout and stderr
	/// redirected, working directory = the DLL folder, a near-empty environment.
	/// </summary>
	static class ProcessorProcess
	{
		/// <summary>The built <c>Distenka.Diagnostics.dll</c> for the configuration these tests were built with.</summary>
		public static string DiagnosticsDll
		{
			get
			{
				var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				var tfm = baseDir.Name;                 // net8.0
				var configuration = baseDir.Parent.Name; // Debug / Release

				var root = baseDir;
				while (root != null && !File.Exists(Path.Combine(root.FullName, "Distenka.sln")))
					root = root.Parent;

				if (root != null)
				{
					var built = Path.Combine(root.FullName, "src", "Diagnostics", "bin", configuration, tfm, "Distenka.Diagnostics.dll");
					if (File.Exists(built))
						return built;
				}

				var copied = Path.Combine(baseDir.FullName, "Distenka.Diagnostics.dll");
				if (File.Exists(copied) && File.Exists(Path.Combine(baseDir.FullName, "Distenka.Diagnostics.runtimeconfig.json")))
					return copied;

				throw new FileNotFoundException("Build src/Diagnostics before running the process-level tests (test.ps1 does).", "Distenka.Diagnostics.dll");
			}
		}

		/// <summary>Writes <paramref name="configJson"/> to a temp file and runs the Diagnostics processor with it.</summary>
		public static Task<ProcessRun> RunDiagnosticsAsync(string configJson, Action<Process> whileRunning = null, int timeoutSeconds = 60)
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath, configJson);

			return RunAsync(DiagnosticsDll, new[] { "run", configPath }, whileRunning, timeoutSeconds);
		}

		public static async Task<ProcessRun> RunAsync(string dll, string[] args, Action<Process> whileRunning, int timeoutSeconds)
		{
			var psi = new ProcessStartInfo(DotnetHost())
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = false,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(dll),
			};

			psi.ArgumentList.Add(dll);
			foreach (var a in args)
				psi.ArgumentList.Add(a);

			// The executor clears the environment (Program.cs:220-243). Keep only what `dotnet` needs to start on the
			// developer's OS; nothing that could make stdout look like a terminal.
			var keep = new[] { "PATH", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_ROOT_X64", "SystemRoot", "windir", "SystemDrive",
				"TEMP", "TMP", "HOME", "USERPROFILE", "ProgramFiles", "ProgramFiles(x86)", "ProgramData", "LOCALAPPDATA", "APPDATA" };
			var saved = keep.Select(k => (k, v: Environment.GetEnvironmentVariable(k))).Where(x => x.v != null).ToList();
			psi.Environment.Clear();
			foreach (var (k, v) in saved)
				psi.Environment[k] = v;
			psi.Environment["DOTNET_NOLOGO"] = "1";

			var stdout = new List<string>();
			var stderr = new List<string>();

			using var process = new Process { StartInfo = psi };
			process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.Add(e.Data); };
			process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderr) stderr.Add(e.Data); };

			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			whileRunning?.Invoke(process);

			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
			try
			{
				await process.WaitForExitAsync(timeout.Token);
			}
			catch (OperationCanceledException)
			{
				process.Kill(entireProcessTree: true);
				throw new TimeoutException($"Processor did not exit within {timeoutSeconds}s.{Environment.NewLine}{string.Join(Environment.NewLine, stdout)}");
			}

			// WaitForExitAsync returns after the redirected streams reach EOF.
			lock (stdout) lock (stderr)
				return new ProcessRun { ExitCode = process.ExitCode, Stdout = stdout.ToList(), Stderr = stderr.ToList() };
		}

		/// <summary>The <c>dotnet</c> host that runs the tests (<c>DOTNET_HOST_PATH</c>), falling back to <c>dotnet</c> on PATH.</summary>
		public static string DotnetHost()
		{
			var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
			return !string.IsNullOrEmpty(host) && File.Exists(host) ? host : "dotnet";
		}
	}
}
