using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The owner's view of one run — <c>GET /api/runs/{runId}/details</c> (<c>docs/api-run-submission.md</c> §Reading the run
	/// back). Lifecycle timestamps are <see langword="null"/> for a stage the run has not reached, and for runs that predate
	/// the platform's state machine. <see cref="Output"/> (and, from DCS-031, the timeline) is the run's own log and result:
	/// there is no separate logs API (AD-13).
	/// </summary>
	public sealed class RunDetails
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunDetails(string? stateName, string? status, DateTimeOffset? createdAt, DateTimeOffset? queuedAt,
			DateTimeOffset? dispatchingAt, DateTimeOffset? startedAt, DateTimeOffset? completedAt, DateTimeOffset? deadlineAt,
			DateTimeOffset? cancelRequestedAt, int? attempt, string? invokedByKeyPrefix, long? totalExecutionMs, string? output)
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

		/// <summary>The run's output — what the processor wrote (capped by the executor).</summary>
		public string? Output { get; }
	}
}
