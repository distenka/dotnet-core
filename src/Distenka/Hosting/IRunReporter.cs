using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// Renders the progress of a <c>run</c> (DCS-041). One implementation per output mode of ADR-009 §2.1:
	/// <see cref="InteractiveSpectreReporter"/> on a terminal, <see cref="PlainLineReporter"/> when stdout is redirected
	/// or <c>--plain</c> is given, and <see cref="NullRunReporter"/> for <c>--silent</c>.
	/// </summary>
	/// <remarks>
	/// Reporters never write <c>ERROR:</c> lines: the single <c>ERROR:</c> line of a failed run is written by
	/// <see cref="RunAction"/> (ADR-004 §2.3 rule 3). Reporters never write to stderr (rule 1).
	/// </remarks>
	internal interface IRunReporter
	{
		/// <summary>Called once before the execution starts.</summary>
		void Starting(string processType);

		/// <summary>
		/// Follows the execution until it has finalized, or until <paramref name="executing"/> completes.
		/// </summary>
		/// <param name="execution">The execution being run.</param>
		/// <param name="log">The result log collecting item results; <c>null</c> when nothing is reported.</param>
		/// <param name="executing">The task running the execution.</param>
		Task MonitorAsync(Execution execution, ResultLog log, Task executing);

		/// <summary>Called once after the execution and all its events have completed.</summary>
		void Completed(Execution execution);
	}

	/// <summary>Writes nothing (<c>run --silent</c>, <see cref="ExecutionConfig.ResultsToConsole"/> = false).</summary>
	internal sealed class NullRunReporter : IRunReporter
	{
		public static readonly NullRunReporter Instance = new NullRunReporter();

		public void Starting(string processType) { }

		public Task MonitorAsync(Execution execution, ResultLog log, Task executing) => Task.CompletedTask;

		public void Completed(Execution execution) { }
	}

	/// <summary>
	/// How <c>run</c> chooses its output mode (ADR-009 §2.1). Registered as a singleton when <c>--plain</c> is given.
	/// </summary>
	internal sealed class RunOutputOptions
	{
		/// <summary>Forces plain mode even when stdout is a terminal (<c>run --plain</c>).</summary>
		public bool ForcePlain { get; init; }

		/// <summary>
		/// Plain mode when forced or when stdout is redirected — always the case under the platform executor.
		/// Decided once at start-up; there is no environment-variable switch (the sandbox has no environment).
		/// </summary>
		public static bool IsPlain(bool forcePlain, bool isOutputRedirected) => forcePlain || isOutputRedirected;

		/// <summary>
		/// Plain mode writes UTF-8 (no BOM). The executor reads the plugin's stdout as UTF-8 (the .NET default on Linux);
		/// on Windows a redirected stdout would otherwise use the OEM code page and mangle non-ASCII text such as the
		/// <c>…</c> of a capped line or a non-ASCII item id. Does nothing when stdout already is UTF-8 or when the
		/// encoding cannot be changed; a writer installed with <see cref="Console.SetOut"/> is kept as is.
		/// </summary>
		public static void UseUtf8Output()
		{
			try
			{
				if (Console.OutputEncoding.CodePage != Encoding.UTF8.CodePage)
					Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
			}
			catch (IOException) { }
			catch (PlatformNotSupportedException) { }
		}
	}

	/// <summary>Rules for user-supplied text that reporters echo (ADR-004 §2.3 rule 2).</summary>
	internal static class ReporterText
	{
		/// <summary>The replacement shown for user text that would trip the executor's error heuristics.</summary>
		public const string Omitted = "(omitted)";

		/// <summary>
		/// Returns <paramref name="text"/> on one physical line, or <see cref="Omitted"/> (and counts it) when it contains a
		/// trigger such as an exception type name used as an item category.
		/// </summary>
		public static string Safe(string text, ref int omitted)
		{
			if (RunOutcome.ContainsErrorTrigger(text))
			{
				omitted++;
				return Omitted;
			}

			return RunOutcome.ToSingleLine(text);
		}
	}
}
