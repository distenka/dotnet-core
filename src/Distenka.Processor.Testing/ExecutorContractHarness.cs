using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Testing
{
	/// <summary>
	/// Runs a built processor package the way the Distenka platform executor does and judges it by the executor's rules
	/// (DCS-093, ADR-004 §2.5): <c>dotnet exec &lt;dll&gt; list</c> as at package validation, and
	/// <c>dotnet &lt;dll&gt; run &lt;config&gt;</c> as for a run — cleared environment, working directory = the DLL folder,
	/// stdout and stderr captured line by line. Use it in a test to check a package before uploading it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The verdicts reproduce the platform's (<see cref="ExecutorRules"/>, pinned to platform commit
	/// <see cref="ExecutorRules.SourceCommit"/>). The <c>Violations</c> go further: they list where the package departs from
	/// the SDK's executor contract (ADR-004) — anything on stderr, a successful run the platform will still mark Failed,
	/// a failed run without its <c>ERROR:</c> line, a missing run summary (DCS-046), an exit code other than 0 or 1, output
	/// beyond the 2000-line cap, or extra text around the <c>list</c> schema.
	/// </para>
	/// <para>
	/// The environment mirrors the sandbox: everything is cleared except what <c>dotnet</c> needs to start on this machine
	/// (<c>PATH</c>, <c>DOTNET_ROOT*</c>, the temp folder and, on Windows, the system and profile folders), plus the executor's own variables —
	/// <c>DOTNET_NOLOGO=1</c>, <c>HOME</c> and <c>DOTNET_CLI_HOME</c> = a temporary folder, the 256 MB heap limit
	/// (<c>COMPlus_GCHeapHardLimit=0x10000000</c>), <c>DOTNET_GCServer=0</c>, <c>DOTNET_GCHeapCount=2</c>.
	/// </para>
	/// </remarks>
	public sealed class ExecutorContractHarness
	{
		static readonly string[] HostVariables =
		{
			"PATH", "DOTNET_ROOT", "DOTNET_ROOT(x86)", "DOTNET_ROOT_X64", "DOTNET_ROOT_ARM64",
			"SystemRoot", "windir", "SystemDrive", "TEMP", "TMP", "ProgramFiles", "ProgramFiles(x86)", "ProgramData",
			"USERPROFILE", "LOCALAPPDATA", "APPDATA",
		};

		/// <summary>Creates a harness for one built processor.</summary>
		/// <param name="dllPath">The processor's main DLL, as the executor finds it in the unpacked package (e.g. <c>tools/net8.0/any/MyJob.dll</c>).</param>
		public ExecutorContractHarness(string dllPath)
		{
			if (string.IsNullOrWhiteSpace(dllPath))
				throw new ArgumentException("The path of the processor's DLL is required.", nameof(dllPath));

			DllPath = Path.GetFullPath(dllPath);
		}

		/// <summary>The processor DLL being judged.</summary>
		public string DllPath { get; }

		/// <summary>
		/// The <c>dotnet</c> host to launch; defaults to <c>DOTNET_HOST_PATH</c> (set by <c>dotnet test</c>), else <c>dotnet</c> on PATH.
		/// </summary>
		public string DotnetHost { get; set; } = DefaultDotnetHost();

		/// <summary>How long <c>list</c> may take; the executor's limit is 60 seconds (<see cref="ExecutorRules.ListTimeout"/>).</summary>
		public TimeSpan ListTimeout { get; set; } = ExecutorRules.ListTimeout;

		/// <summary>How long <c>run</c> may take before the harness kills it (the platform's run deadline is deployment-specific).</summary>
		public TimeSpan RunTimeout { get; set; } = TimeSpan.FromMinutes(5);

		/// <summary>The platform's schema nesting limit (<c>MaxSchemaDepth</c>, deployment-configurable).</summary>
		public int MaxSchemaDepth { get; set; } = ExecutorRules.DefaultMaxSchemaDepth;

		/// <summary>Extra environment variables for the spawned process (the executor sets none beyond its own).</summary>
		public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

		/// <summary>
		/// Runs <c>dotnet exec &lt;dll&gt; list</c> and judges it as package validation does (ADR-003 §1.1): exit code,
		/// the first balanced JSON object on stdout, and whether the platform can compile it.
		/// </summary>
		public async Task<ListVerdict> ListAsync(CancellationToken cancellationToken = default)
		{
			var captured = await SpawnAsync(new[] { "exec", DllPath, "list" }, ListTimeout, cancellationToken).ConfigureAwait(false);
			return EvaluateList(captured.ExitCode, captured.TimedOut, captured.Lines, MaxSchemaDepth);
		}

		/// <summary>
		/// Writes <paramref name="configurationJson"/> to a temporary file, runs <c>dotnet &lt;dll&gt; run &lt;file&gt;</c> and
		/// judges the run as the executor does.
		/// </summary>
		/// <param name="configurationJson">The run configuration document, as a caller would submit it (no <c>process</c> needed for a single-process package).</param>
		/// <param name="cancellationToken">Kills the process and throws <see cref="OperationCanceledException"/>.</param>
		public async Task<RunVerdict> RunAsync(string configurationJson, CancellationToken cancellationToken = default)
		{
			if (configurationJson is null)
				throw new ArgumentNullException(nameof(configurationJson));

			// The executor writes the configuration to /tmp/distenka_{runId}_*.json (Program.cs:180-183).
			var configPath = Path.Combine(Path.GetTempPath(), $"distenka_{Guid.NewGuid():N}.json");
			await File.WriteAllTextAsync(configPath, configurationJson, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

			try
			{
				var captured = await SpawnAsync(new[] { DllPath, "run", configPath }, RunTimeout, cancellationToken).ConfigureAwait(false);
				return EvaluateRun(captured.ExitCode, captured.TimedOut, captured.Lines);
			}
			finally
			{
				try { File.Delete(configPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
			}
		}

		/// <summary>Judges a finished <c>list</c>. Separate from spawning so the rules can be tested on recorded output.</summary>
		internal static ListVerdict EvaluateList(int exitCode, bool timedOut, IReadOnlyList<CapturedLine> lines, int maxSchemaDepth)
		{
			var stdout = lines.Where(l => !l.IsStderr).Select(l => l.Text).ToList();
			var stderr = lines.Where(l => l.IsStderr).Select(l => l.Text).ToList();
			var violations = new List<string>();
			string rejection = null;
			string schema = null;
			IReadOnlyList<string> schemaErrors = Array.Empty<string>();

			if (timedOut)
			{
				rejection = $"The package did not describe its configuration within {ExecutorRules.ListTimeout.TotalSeconds:0} seconds.";
				violations.Add("`list` did not finish within the executor's time limit");
			}
			else if (exitCode != 0)
			{
				// Program.cs:649-657 — stdout is not consulted on failure.
				var quoted = string.Join("\n", stderr);
				if (quoted.Length > ExecutorRules.ListStderrQuoteLength)
					quoted = quoted.Substring(0, ExecutorRules.ListStderrQuoteLength);
				rejection = $"The package exited with code {exitCode} when asked to describe its configuration. {quoted}".TrimEnd();
				violations.Add($"`list` exited with code {exitCode}");
			}
			else
			{
				var text = string.Join("\n", stdout);
				var candidate = ExecutorRules.ExtractJsonObject(text);

				if (candidate is null || !IsJsonObject(candidate))
				{
					rejection = "The package produced no JSON configuration object on standard output.";
					violations.Add("`list` printed no usable JSON object");
				}
				else
				{
					schema = candidate;
					schemaErrors = DescriptorSchemaValidator.Validate(candidate, maxSchemaDepth);
					violations.AddRange(schemaErrors.Select(e => "schema unusable: " + e));

					// ADR-003 §2.1: `list` prints exactly one JSON object and nothing else on stdout.
					if (text.Trim() != candidate)
						violations.Add("`list` printed text besides the schema object on stdout (ADR-003 §2.1)");
				}

				if (stderr.Count > 0)
					violations.Add($"`list` wrote {stderr.Count} line(s) to stderr on success: {stderr[0]}");
			}

			return new ListVerdict(timedOut ? -1 : exitCode, timedOut, rejection is null, schema, schemaErrors, rejection, stdout, stderr, violations);
		}

		/// <summary>Judges a finished <c>run</c>. Separate from spawning so the rules can be tested on recorded output.</summary>
		internal static RunVerdict EvaluateRun(int exitCode, bool timedOut, IReadOnlyList<CapturedLine> lines)
		{
			if (timedOut)
				exitCode = -1;

			var stdout = lines.Where(l => !l.IsStderr).Select(l => l.Text).ToList();
			var stderr = lines.Where(l => l.IsStderr).Select(l => l.Text).ToList();
			var errorStdout = stdout.Where(ExecutorRules.IsErrorStdoutLine).ToList();

			var hasPluginErrors = stderr.Count > 0 || errorStdout.Count > 0;
			var pluginSuccess = ExecutorRules.PluginSuccess(exitCode, hasPluginErrors);
			var output = string.Join("\n", lines.Take(ExecutorRules.MaxOutputLines).Select(l => l.Text));

			var summaryLine = stdout.Count > 0 && IsSummaryLine(stdout[stdout.Count - 1]) ? stdout[stdout.Count - 1] : null;
			var disposition = SummaryDispositionOf(summaryLine);

			var violations = new List<string>();

			if (timedOut)
				violations.Add("the run did not finish within the harness's RunTimeout and was killed");
			else if (exitCode != 0 && exitCode != 1)
				violations.Add($"exit code {exitCode} is outside the SDK contract (0 or 1, ADR-004 §2.1)");

			if (stderr.Count > 0)
				violations.Add($"{stderr.Count} line(s) on stderr — the executor treats each as an error (ADR-004 §2.3 rule 1): {stderr[0]}");

			if (exitCode == 0)
			{
				foreach (var line in errorStdout.Take(10))
					violations.Add($"exit 0, but this stdout line trips the executor's error rules, so the platform marks the run Failed: {line}");
				if (errorStdout.Count > 10)
					violations.Add($"{errorStdout.Count - 10} more stdout line(s) trip the executor's error rules");
			}

			if (!timedOut && exitCode != 0 && !stdout.Any(l => l.StartsWith("ERROR:", StringComparison.Ordinal)))
				violations.Add("the run failed without an `ERROR:` line stating why (ADR-004 §2.3 rule 3)");

			if (!timedOut && (exitCode == 0 || exitCode == 1))
			{
				if (summaryLine is null)
					violations.Add("the last stdout line is not the run summary (DCS-046)");
				else if ((exitCode == 0) != (disposition == "Successful"))
					violations.Add($"exit code {exitCode} disagrees with the summary's disposition '{disposition}'");
			}

			if (lines.Count > ExecutorRules.MaxOutputLines)
				violations.Add($"the run wrote {lines.Count} lines; the platform keeps the first {ExecutorRules.MaxOutputLines}, so the end — including the summary — is lost (ADR-004 §2.4)");

			return new RunVerdict(exitCode, timedOut, ExecutorRules.InfraSuccess(exitCode), pluginSuccess, ExecutorRules.FinalState(pluginSuccess),
				hasPluginErrors, output, stdout, stderr, summaryLine, disposition, violations);
		}

		static bool IsSummaryLine(string line) => line.StartsWith("{\"distenkaSummary\":", StringComparison.Ordinal);

		static string SummaryDispositionOf(string summaryLine)
		{
			if (summaryLine is null)
				return null;

			try
			{
				using var document = JsonDocument.Parse(summaryLine);
				return document.RootElement.TryGetProperty("disposition", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		static bool IsJsonObject(string text)
		{
			try
			{
				using var document = JsonDocument.Parse(text);
				return document.RootElement.ValueKind == JsonValueKind.Object;
			}
			catch (JsonException)
			{
				return false;
			}
		}

		async Task<Captured> SpawnAsync(string[] arguments, TimeSpan timeout, CancellationToken cancellationToken)
		{
			if (!File.Exists(DllPath))
				throw new FileNotFoundException("The processor DLL does not exist. Build the processor first.", DllPath);

			var psi = new ProcessStartInfo(DotnetHost)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = false,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(DllPath),
				// The executor (Linux .NET) reads the plugin's streams as UTF-8.
				StandardOutputEncoding = new UTF8Encoding(false),
				StandardErrorEncoding = new UTF8Encoding(false),
			};

			foreach (var argument in arguments)
				psi.ArgumentList.Add(argument);

			var keep = HostVariables
				.Select(name => (name, value: System.Environment.GetEnvironmentVariable(name)))
				.Where(x => x.value != null)
				.ToList();
			psi.Environment.Clear();
			foreach (var (name, value) in keep)
				psi.Environment[name] = value;

			// The executor's own sandbox variables (Program.cs:232-242).
			var home = Path.GetTempPath();
			psi.Environment["DOTNET_NOLOGO"] = "1";
			psi.Environment["DOTNET_CLI_HOME"] = home;
			psi.Environment["HOME"] = home;
			psi.Environment["COMPlus_GCHeapHardLimit"] = "0x10000000";
			psi.Environment["DOTNET_GCServer"] = "0";
			psi.Environment["DOTNET_GCHeapCount"] = "2";
			foreach (var pair in Environment)
				psi.Environment[pair.Key] = pair.Value;

			var lines = new List<CapturedLine>();

			using var process = new Process { StartInfo = psi };
			process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (lines) lines.Add(new CapturedLine(false, e.Data)); };
			process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (lines) lines.Add(new CapturedLine(true, e.Data)); };

			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			deadline.CancelAfter(timeout);

			var timedOut = false;
			try
			{
				await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
				cancellationToken.ThrowIfCancellationRequested();
				timedOut = true;
				await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
			}

			// WaitForExitAsync returns after the redirected streams reach EOF.
			lock (lines)
				return new Captured(timedOut ? -1 : process.ExitCode, timedOut, lines.ToList());
		}

		static string DefaultDotnetHost()
		{
			var host = System.Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
			return !string.IsNullOrEmpty(host) && File.Exists(host) ? host : "dotnet";
		}

		sealed class Captured
		{
			public Captured(int exitCode, bool timedOut, IReadOnlyList<CapturedLine> lines)
			{
				ExitCode = exitCode;
				TimedOut = timedOut;
				Lines = lines;
			}

			public int ExitCode { get; }
			public bool TimedOut { get; }
			public IReadOnlyList<CapturedLine> Lines { get; }
		}
	}

	/// <summary>One captured output line, in arrival order across both streams.</summary>
	internal readonly struct CapturedLine
	{
		public CapturedLine(bool isStderr, string text)
		{
			IsStderr = isStderr;
			Text = text;
		}

		public bool IsStderr { get; }
		public string Text { get; }
	}

	/// <summary>How the platform would judge a package's <c>list</c> at validation (<see cref="ExecutorContractHarness.ListAsync"/>).</summary>
	public sealed class ListVerdict
	{
		internal ListVerdict(int exitCode, bool timedOut, bool accepted, string schema, IReadOnlyList<string> schemaErrors, string rejection,
			IReadOnlyList<string> stdout, IReadOnlyList<string> stderr, IReadOnlyList<string> violations)
		{
			ExitCode = exitCode;
			TimedOut = timedOut;
			Accepted = accepted;
			Schema = schema;
			SchemaErrors = schemaErrors;
			Rejection = rejection;
			Stdout = stdout;
			Stderr = stderr;
			Violations = violations;
		}

		/// <summary>The process exit code; <c>-1</c> when the harness killed it at the time limit.</summary>
		public int ExitCode { get; }

		/// <summary>Whether <c>list</c> ran past the time limit.</summary>
		public bool TimedOut { get; }

		/// <summary>Whether package validation succeeds: <c>list</c> exited 0 and printed a JSON object the platform pins as the schema.</summary>
		public bool Accepted { get; }

		/// <summary>
		/// Whether the platform can compile the schema — without this the package uploads but cannot be invoked
		/// (<c>schema.unusable</c>).
		/// </summary>
		public bool SchemaUsable => Accepted && SchemaErrors.Count == 0;

		/// <summary>The JSON object the executor takes from stdout (<see cref="ExecutorRules.ExtractJsonObject"/>); <c>null</c> when none.</summary>
		public string Schema { get; }

		/// <summary>Why the schema is unusable (<see cref="DescriptorSchemaValidator"/>); empty when usable.</summary>
		public IReadOnlyList<string> SchemaErrors { get; }

		/// <summary>Why package validation fails, in the executor's words where known; <c>null</c> when it succeeds.</summary>
		public string Rejection { get; }

		/// <summary>The stdout lines.</summary>
		public IReadOnlyList<string> Stdout { get; }

		/// <summary>The stderr lines.</summary>
		public IReadOnlyList<string> Stderr { get; }

		/// <summary>Departures from the SDK's <c>list</c> contract (ADR-003); empty when the package complies.</summary>
		public IReadOnlyList<string> Violations { get; }

		/// <summary>A readable account of the verdict, for assertion messages.</summary>
		public override string ToString() =>
			$"list: exit {ExitCode}, accepted {Accepted}, schema usable {SchemaUsable}" +
			(Rejection is null ? "" : $"{System.Environment.NewLine}rejection: {Rejection}") +
			Section("violations", Violations) + Section("stdout", Stdout) + Section("stderr", Stderr);

		internal static string Section(string title, IReadOnlyList<string> lines) =>
			lines.Count == 0 ? "" : $"{System.Environment.NewLine}--- {title} ---{System.Environment.NewLine}{string.Join(System.Environment.NewLine, lines)}";
	}

	/// <summary>How the platform would judge a run (<see cref="ExecutorContractHarness.RunAsync"/>).</summary>
	public sealed class RunVerdict
	{
		internal RunVerdict(int exitCode, bool timedOut, bool infraSuccess, bool? pluginSuccess, string finalState, bool hasPluginErrors,
			string output, IReadOnlyList<string> stdout, IReadOnlyList<string> stderr, string summaryLine, string summaryDisposition,
			IReadOnlyList<string> violations)
		{
			ExitCode = exitCode;
			TimedOut = timedOut;
			InfraSuccess = infraSuccess;
			PluginSuccess = pluginSuccess;
			FinalState = finalState;
			HasPluginErrors = hasPluginErrors;
			Output = output;
			Stdout = stdout;
			Stderr = stderr;
			SummaryLine = summaryLine;
			SummaryDisposition = summaryDisposition;
			Violations = violations;
		}

		/// <summary>The process exit code; <c>-1</c> when the harness killed it at <see cref="ExecutorContractHarness.RunTimeout"/>.</summary>
		public int ExitCode { get; }

		/// <summary>Whether the run was killed at the time limit.</summary>
		public bool TimedOut { get; }

		/// <summary>The executor's <c>infraSuccess</c>: <c>exitCode != -1</c>.</summary>
		public bool InfraSuccess { get; }

		/// <summary>The executor's <c>pluginSuccess</c>: exit 0 and no line that trips its error rules; <c>null</c> without infra success.</summary>
		public bool? PluginSuccess { get; }

		/// <summary>The platform state the run would end in: <c>Completed</c> or <c>Failed</c>.</summary>
		public string FinalState { get; }

		/// <summary>The executor's <c>hasPluginErrors</c>: any stderr line, or a stdout line its error rules match.</summary>
		public bool HasPluginErrors { get; }

		/// <summary>The run's <c>output</c> as the platform would store it: the first 2000 lines of stdout and stderr, joined by <c>\n</c>.</summary>
		public string Output { get; }

		/// <summary>All stdout lines.</summary>
		public IReadOnlyList<string> Stdout { get; }

		/// <summary>All stderr lines.</summary>
		public IReadOnlyList<string> Stderr { get; }

		/// <summary>The run summary (DCS-046) when it is the last stdout line; <c>null</c> otherwise.</summary>
		public string SummaryLine { get; }

		/// <summary>The summary's <c>disposition</c> (<c>Successful</c>, <c>Failed</c>, <c>Cancelled</c>); <c>null</c> without a summary.</summary>
		public string SummaryDisposition { get; }

		/// <summary>Departures from the SDK's executor contract (ADR-004); empty when the run complies.</summary>
		public IReadOnlyList<string> Violations { get; }

		/// <summary>A readable account of the verdict, for assertion messages.</summary>
		public override string ToString() =>
			$"run: exit {ExitCode}, plugin success {PluginSuccess?.ToString() ?? "null"}, state {FinalState}, summary {SummaryDisposition ?? "none"}" +
			ListVerdict.Section("violations", Violations) + ListVerdict.Section("stdout", Stdout) + ListVerdict.Section("stderr", Stderr);
	}
}
