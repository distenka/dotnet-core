using Distenka.Client.Serialization;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// A run's lifecycle state — the platform's nine <c>RunState</c> names (<c>docs/api-run-submission.md</c>
	/// §Reading the run back). Read tolerantly: a state the SDK does not know yet becomes <see cref="Unknown"/>
	/// rather than failing (ADR-008 §2.2).
	/// </summary>
	[JsonConverter(typeof(RunStateJsonConverter))]
	public enum RunState
	{
		/// <summary>A state this version of the SDK does not know (the platform added one), or none was given.</summary>
		Unknown = 0,

		/// <summary>Accepted (<c>202</c>) and durably recorded; not yet queued.</summary>
		Accepted,

		/// <summary>Waiting in the queue to be dispatched.</summary>
		Queued,

		/// <summary>Claimed and being handed to Kubernetes.</summary>
		Dispatching,

		/// <summary>The processor is running.</summary>
		Running,

		/// <summary>Terminal: finished successfully.</summary>
		Completed,

		/// <summary>Terminal: finished with a failure.</summary>
		Failed,

		/// <summary>Terminal: cancelled.</summary>
		Cancelled,

		/// <summary>Terminal: exceeded its run time.</summary>
		TimedOut,

		/// <summary>Terminal: waited in the queue past its deadline and never started (no charge).</summary>
		Expired,
	}

	/// <summary>Helpers for <see cref="RunState"/>.</summary>
	public static class RunStateExtensions
	{
		/// <summary>
		/// Whether the run has ended: <see cref="RunState.Completed"/>, <see cref="RunState.Failed"/>,
		/// <see cref="RunState.Cancelled"/>, <see cref="RunState.TimedOut"/> or <see cref="RunState.Expired"/>.
		/// <see cref="RunState.Unknown"/> is not terminal.
		/// </summary>
		public static bool IsTerminal(this RunState state) =>
			state == RunState.Completed || state == RunState.Failed || state == RunState.Cancelled ||
			state == RunState.TimedOut || state == RunState.Expired;
	}
}
