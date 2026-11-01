using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The owner's view of one run — <c>GET /api/runs/{runId}/details</c> (<c>docs/api-run-submission.md</c> §Reading the run
	/// back). Lifecycle timestamps are <see langword="null"/> for a stage the run has not reached, and for runs that predate
	/// the platform's state machine. <see cref="Timeline"/> and <see cref="Output"/> are the run's own log and result —
	/// the SDK's only execution-log surface: there is no logs endpoint (AD-13, ADR-007).
	/// </summary>
	public sealed class RunDetails
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunDetails(string? stateName, string? status, DateTimeOffset? createdAt, DateTimeOffset? queuedAt,
			DateTimeOffset? dispatchingAt, DateTimeOffset? startedAt, DateTimeOffset? completedAt, DateTimeOffset? deadlineAt,
			DateTimeOffset? cancelRequestedAt, int? attempt, string? invokedByKeyPrefix, long? totalExecutionMs, string? output,
			IReadOnlyList<RunTimelineEntry>? timeline)
		{
			StateName = stateName;
			Status = status;
			CreatedAt = createdAt;
			QueuedAt = queuedAt;
			DispatchingAt = dispatchingAt;
			StartedAt = startedAt;
			CompletedAt = completedAt;
			DeadlineAt = deadlineAt;
			CancelRequestedAt = cancelRequestedAt;
			Attempt = attempt;
			InvokedByKeyPrefix = invokedByKeyPrefix;
			TotalExecutionMs = totalExecutionMs;
			Output = output;
			Timeline = timeline ?? Array.Empty<RunTimelineEntry>();
		}

		/// <summary>The run's state; <see cref="RunState.Unknown"/> when the platform sent a name this SDK does not know (see <see cref="StateName"/>).</summary>
		[JsonIgnore]
		public RunState State => Serialization.RunStateNames.Parse(StateName);

		/// <summary>The <c>state</c> exactly as the platform sent it — kept so a state newer than this SDK is not lost.</summary>
		[JsonPropertyName("state")]
		public string? StateName { get; }

		/// <summary>The live/archived projection's status word (e.g. <c>Pending</c>); may differ from <see cref="State"/>.</summary>
		public string? Status { get; }

		/// <summary>Accepted (<c>202</c>).</summary>
		public DateTimeOffset? CreatedAt { get; }

		/// <summary>Entered the queue for this attempt.</summary>
		public DateTimeOffset? QueuedAt { get; }

		/// <summary>Claimed and handed to Kubernetes.</summary>
		public DateTimeOffset? DispatchingAt { get; }

		/// <summary>Entered <see cref="RunState.Running"/>.</summary>
		public DateTimeOffset? StartedAt { get; }

		/// <summary>Reached a terminal state.</summary>
		public DateTimeOffset? CompletedAt { get; }

		/// <summary>The deadline as it stands: the queue deadline while waiting, cleared at dispatch.</summary>
		public DateTimeOffset? DeadlineAt { get; }

		/// <summary>When cancellation was asked for.</summary>
		public DateTimeOffset? CancelRequestedAt { get; }

		/// <summary>The 1-based attempt number.</summary>
		public int? Attempt { get; }

		/// <summary>The public prefix (<c>DKI-…</c>) of the invocation key that started the run; <see langword="null"/> for an owner's run.</summary>
		public string? InvokedByKeyPrefix { get; }

		/// <summary><see cref="CompletedAt"/> − <see cref="StartedAt"/>, in milliseconds.</summary>
		public long? TotalExecutionMs { get; }

		/// <summary>
		/// The run's captured output (stdout and stderr) as one text, exactly as the platform stored it. The executor keeps
		/// at most <b>2000 lines</b>, so this is not a complete log archive: item-level results belong in a results file.
		/// Together with <see cref="Timeline"/> it is the execution-log contract (AD-13); there is no logs endpoint.
		/// </summary>
		public string? Output { get; }

		/// <summary>
		/// The run's log as entries — one per line the processor wrote, with its level and elapsed time — built by the
		/// platform from the captured output (so bounded by the same 2000-line cap). Empty when the platform sent none.
		/// For runs archived before structured logs, entries come from plain text and have no <see cref="RunTimelineEntry.Level"/>.
		/// </summary>
		public IReadOnlyList<RunTimelineEntry> Timeline { get; }
	}
}
