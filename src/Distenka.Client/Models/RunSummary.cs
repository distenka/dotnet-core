using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The calling account's runs counted by state, and the last 24 hours — <c>GET /api/runs/summary</c>
	/// (<c>docs/api-run-submission.md</c> §Counting your runs).
	/// </summary>
	public sealed class RunSummary
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunSummary(IReadOnlyDictionary<string, long>? byState, RunSummaryLast24Hours? last24h, DateTimeOffset? asOf)
		{
			ByState = byState ?? new Dictionary<string, long>();
			Last24h = last24h;
			AsOf = asOf;
		}

		/// <summary>
		/// Every state name with the account's count (0 included), keyed exactly as the platform writes them
		/// (e.g. <c>"Queued"</c>). Kept as names, not <see cref="RunState"/>, so a newer state is still counted.
		/// </summary>
		public IReadOnlyDictionary<string, long> ByState { get; }

		/// <summary>What happened in the 24 hours up to <see cref="AsOf"/>.</summary>
		public RunSummaryLast24Hours? Last24h { get; }

		/// <summary>When the counts were taken (UTC).</summary>
		public DateTimeOffset? AsOf { get; }
	}

	/// <summary>The <c>last24h</c> block of <see cref="RunSummary"/>.</summary>
	public sealed class RunSummaryLast24Hours
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunSummaryLast24Hours(long accepted, long completed, long failed, long timedOut, long expired, long cancelled)
		{
			Accepted = accepted;
			Completed = completed;
			Failed = failed;
			TimedOut = timedOut;
			Expired = expired;
			Cancelled = cancelled;
		}

		/// <summary>Runs created in the window, whatever their state now.</summary>
		public long Accepted { get; }

		/// <summary>Runs that completed in the window.</summary>
		public long Completed { get; }

		/// <summary>Runs that failed in the window.</summary>
		public long Failed { get; }

		/// <summary>Runs that timed out in the window.</summary>
		public long TimedOut { get; }

		/// <summary>Runs that expired in the queue in the window.</summary>
		public long Expired { get; }

		/// <summary>Runs cancelled in the window.</summary>
		public long Cancelled { get; }
	}
}
