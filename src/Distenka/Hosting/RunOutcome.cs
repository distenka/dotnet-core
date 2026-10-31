using System;
using System.Text;

namespace Distenka.Hosting
{
	/// <summary>
	/// Maps the end of a <c>run</c> to the platform executor contract (ADR-004): the process exit code and the single
	/// <c>ERROR:</c> line that states why a run did not succeed.
	/// </summary>
	/// <remarks>
	/// The executor judges a plugin by <c>exitCode == 0 &amp;&amp; !hasPluginErrors</c> only
	/// (<c>Distenka.Core.Executor/Program.cs:326-328</c> @ <c>4c3cf2c</c>). There are exactly two SDK exit codes
	/// (ADR-004 §2.1); Failed and Cancelled are distinguished in band, never by exit code.
	/// </remarks>
	internal static class RunOutcome
	{
		/// <summary>Exit code for a run that ended <see cref="Disposition.Successful"/> with a clean success path.</summary>
		public const int SuccessExitCode = 0;

		/// <summary>Exit code for every other outcome the SDK controls: Failed, Cancelled, startup error, unhandled exception.</summary>
		public const int FailureExitCode = 1;

		/// <summary>Maximum length of one stdout line written by the SDK (ADR-009 §2.2).</summary>
		public const int MaxLineLength = 8192;

		/// <summary>
		/// Gets the process exit code for a finished execution (ADR-004 §2.1).
		/// </summary>
		public static int ExitCodeFor(Disposition disposition) =>
			disposition == Disposition.Successful ? SuccessExitCode : FailureExitCode;

		/// <summary>
		/// Describes why a finished execution did not succeed, as one <c>ERROR:</c> line (ADR-004 §2.2 rows 3, 4 and 6),
		/// or returns <c>null</c> when the execution ended <see cref="Disposition.Successful"/>.
		/// </summary>
		/// <param name="execution">The finished execution.</param>
		/// <param name="hostStopRequested">Whether the host asked the run to stop (Ctrl+C, SIGTERM, <c>StopApplication</c>).</param>
		public static string DescribeFailure(Execution execution, bool hostStopRequested)
		{
			if (execution is null)
				throw new ArgumentNullException(nameof(execution));

			switch (execution.Disposition)
			{
				case Disposition.Successful:
					return null;

				case Disposition.Cancelled:
					return ErrorLine(hostStopRequested
						? "run cancelled (host shutdown requested)"
						: "run cancelled (cancellation requested by the processor)");

				default:
					return ErrorLine(DescribeFailedExecution(execution));
			}
		}

		/// <summary>
		/// Describes an exception that escaped the run pipeline (ADR-004 §2.2 rows 5 and 7) as one <c>ERROR:</c> line.
		/// </summary>
		/// <param name="exception">The exception that escaped.</param>
		/// <param name="hostStopRequested">Whether the host asked the run to stop.</param>
		public static string DescribeUnhandled(Exception exception, bool hostStopRequested)
		{
			if (exception is OperationCanceledException && hostStopRequested)
				return ErrorLine("run cancelled (host shutdown requested)");

			return ErrorLine("unhandled " + DescribeException(exception));
		}

		static string DescribeFailedExecution(Execution execution)
		{
			var exception = execution.FailedBecauseOf;

			if (execution.FailedIn == ProcessorMethod.ProcessAsync)
			{
				var threshold = Math.Max(1, execution.Processor?.Config?.Execution?.ItemFailureCountToStopProcess ?? 1);
				var reason = $"item failure threshold reached: {execution.FailedItemCount} failed item(s), ItemFailureCountToStopProcess={threshold}";

				return exception is null ? reason : $"{reason}; last: {DescribeException(exception)}";
			}

			if (execution.FailedIn.HasValue)
				return exception is null
					? $"{execution.FailedIn.Value} failed"
					: $"{execution.FailedIn.Value}: {DescribeException(exception)}";

			return exception is null ? "processor failed" : $"processor failed: {DescribeException(exception)}";
		}

		static string DescribeException(Exception exception) =>
			exception is null ? "unknown error" : $"{exception.GetType().FullName}: {exception.Message}";

		/// <summary>
		/// Builds an <c>ERROR:</c> line: one physical line, escaped and capped as ADR-009 §2.2 requires.
		/// </summary>
		public static string ErrorLine(string reason) => ToSingleLine("ERROR: " + reason);

		/// <summary>
		/// Indicates whether <paramref name="text"/> would make the executor flag the run as failed when written to stdout:
		/// it contains <c>ERROR:</c>/<c>error:</c> in any case, or <c>Exception</c> (case-sensitive) — <c>ParseLogLevel</c>,
		/// <c>Program.cs:427-433</c> and <c>:245-260</c> @ <c>4c3cf2c</c>.
		/// </summary>
		public static bool ContainsErrorTrigger(string text) =>
			!string.IsNullOrEmpty(text) &&
			(text.IndexOf("error:", StringComparison.OrdinalIgnoreCase) >= 0 ||
			 text.IndexOf("Exception", StringComparison.Ordinal) >= 0);

		/// <summary>
		/// Escapes <paramref name="text"/> onto one physical line: <c>\r</c>/<c>\n</c> become the two characters
		/// <c>\r</c>/<c>\n</c>, every other C0 control character becomes <c>\uXXXX</c>, and the result is capped at
		/// <see cref="MaxLineLength"/> characters (ADR-009 §2.2).
		/// </summary>
		public static string ToSingleLine(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			var sb = new StringBuilder(text.Length);

			foreach (var c in text)
			{
				switch (c)
				{
					case '\r': sb.Append("\\r"); break;
					case '\n': sb.Append("\\n"); break;
					default:
						if (c < 0x20 || c == 0x7F)
							sb.Append("\\u").Append(((int)c).ToString("X4"));
						else
							sb.Append(c);
						break;
				}
			}

			if (sb.Length <= MaxLineLength)
				return sb.ToString();

			// The suffix states how many characters were cut; its own length depends on that number, so settle it first.
			var removed = sb.Length - MaxLineLength;
			string suffix;
			int keep;

			while (true)
			{
				suffix = $"… (+{removed} chars)";
				keep = MaxLineLength - suffix.Length;

				if (sb.Length - keep == removed)
					break;

				removed = sb.Length - keep;
			}

			// Never split a surrogate pair.
			if (char.IsHighSurrogate(sb[keep - 1]))
			{
				keep--;
				suffix = $"… (+{sb.Length - keep} chars)";
			}

			return sb.ToString(0, keep) + suffix;
		}
	}
}
