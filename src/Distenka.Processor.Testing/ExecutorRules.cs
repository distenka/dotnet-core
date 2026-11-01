using System;
using System.Text.RegularExpressions;

namespace Distenka.Testing
{
	/// <summary>
	/// The Distenka platform executor's rules for judging a processor package, reproduced verbatim (DCS-093, ADR-004 §2.5,
	/// AD-3) so a package can be checked before it is uploaded.
	/// </summary>
	/// <remarks>
	/// Mirrors <c>Distenka.Core.Executor/Program.cs</c> at platform commit <see cref="SourceCommit"/> (file last changed in
	/// <see cref="ProgramLastChangedCommit"/>): stdout handler <c>:245-260</c>, stderr handler <c>:262-276</c>, verdict
	/// <c>:326-328</c>, final state <c>:357-359</c>, <c>ParseLogLevel</c> <c>:427-433</c>, output cap <c>:196-199</c> /
	/// <c>:435-442</c>, package validation <c>:558-660</c> and <c>ExtractJsonObject</c> <c>:676-704</c>. Re-verify whenever
	/// that file changes: these are the executor's heuristics, not a specification.
	/// </remarks>
	public static class ExecutorRules
	{
		/// <summary>The platform commit (<c>D:\Product\Distenka</c>) these rules were read from.</summary>
		public const string SourceCommit = "4c3cf2c";

		/// <summary>The commit that last changed <c>Distenka.Core.Executor/Program.cs</c> before <see cref="SourceCommit"/>.</summary>
		public const string ProgramLastChangedCommit = "cdd1a54";

		/// <summary>The executor keeps the first 2000 lines of a run's output (<c>MaxOutputLines</c>, <c>:199</c>).</summary>
		public const int MaxOutputLines = 2000;

		/// <summary>How long the executor waits for <c>list</c> at package validation (<c>:558-645</c>).</summary>
		public static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(60);

		/// <summary>How much of stderr the executor quotes when <c>list</c> exits non-zero (<c>:649-657</c>).</summary>
		public const int ListStderrQuoteLength = 2000;

		/// <summary>The schema nesting limit the platform applies by default (<c>MaxSchemaDepth</c>, deployment-configurable).</summary>
		public const int DefaultMaxSchemaDepth = 16;

		/// <summary>
		/// <c>ParseLogLevel</c> (<c>:427-433</c>): <c>ERROR:</c> anywhere (any case) → <c>ERROR</c>; <c>WARNING:</c>/<c>WARN:</c>
		/// (any case) → <c>WARNING</c>; <c>Exception</c> (case-sensitive) or <c>error:</c> (lower case) → <c>ERROR</c>;
		/// otherwise <c>INFO</c>.
		/// </summary>
		public static string ParseLogLevel(string line)
		{
			if (line is null)
				throw new ArgumentNullException(nameof(line));

			if (Regex.IsMatch(line, @"ERROR:", RegexOptions.IgnoreCase)) return "ERROR";
			if (Regex.IsMatch(line, @"(WARNING|WARN):", RegexOptions.IgnoreCase)) return "WARNING";
			if (line.Contains("Exception") || line.Contains("error:")) return "ERROR";
			return "INFO";
		}

		/// <summary>
		/// Whether a stdout line sets <c>hasPluginErrors</c> (<c>:245-260</c>: <c>level == "ERROR" || line.Contains("Exception")</c>).
		/// Every stderr line sets it (<c>:262-276</c>).
		/// </summary>
		public static bool IsErrorStdoutLine(string line) =>
			ParseLogLevel(line) == "ERROR" || line.Contains("Exception");

		/// <summary><c>infraSuccess = exitCode != -1</c> (<c>:326</c>).</summary>
		public static bool InfraSuccess(int exitCode) => exitCode != -1;

		/// <summary>
		/// <c>pluginSuccess = infraSuccess ? (exitCode == 0 &amp;&amp; !hasPluginErrors) : null</c> (<c>:327-328</c>).
		/// </summary>
		public static bool? PluginSuccess(int exitCode, bool hasPluginErrors) =>
			InfraSuccess(exitCode) ? exitCode == 0 && !hasPluginErrors : (bool?)null;

		/// <summary>The run's final platform state (<c>:357-359</c>): <c>Completed</c> only when the plugin succeeded, otherwise <c>Failed</c>.</summary>
		public static string FinalState(bool? pluginSuccess) => pluginSuccess == true ? "Completed" : "Failed";

		/// <summary>
		/// <c>ExtractJsonObject</c> (<c>:676-704</c>): from the first <c>{</c> to its balancing <c>}</c>, counting raw braces
		/// <b>without</b> regard to JSON strings (a brace inside a string value shifts the balance). <c>null</c> when there is none.
		/// </summary>
		public static string ExtractJsonObject(string stdout)
		{
			if (stdout is null)
				return null;

			var start = stdout.IndexOf('{');
			if (start < 0)
				return null;

			var depth = 0;
			for (var i = start; i < stdout.Length; i++)
			{
				if (stdout[i] == '{')
					depth++;
				else if (stdout[i] == '}' && --depth == 0)
					return stdout.Substring(start, i - start + 1);
			}

			return null;
		}
	}
}
