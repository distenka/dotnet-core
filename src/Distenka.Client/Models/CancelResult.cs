using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The body of <c>POST /api/runs/{runId}/cancel</c> — <c>200 OK</c> means the cancellation request stands
	/// (<c>docs/api-run-cancellation.md</c>). The same body is returned for a repeated cancel; read the run's details to see
	/// its state. When the run had already ended (<c>409 run_already_ended</c>), <see cref="IRunsClient.CancelAsync"/> returns
	/// a result with <see cref="AlreadyEnded"/> set instead.
	/// </summary>
	public sealed class CancelResult
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public CancelResult(string runId, string? status, DateTimeOffset? cancelRequestedAt)
		{
			RunId = runId;
			Status = status;
			CancelRequestedAt = cancelRequestedAt;
		}

		/// <summary>Creates the value including the outcomes that do not come from the <c>200</c> body.</summary>
		public CancelResult(string runId, string? status, DateTimeOffset? cancelRequestedAt, bool alreadyEnded, RunState? endedState)
			: this(runId, status, cancelRequestedAt)
		{
			AlreadyEnded = alreadyEnded;
			EndedState = endedState;
		}

		/// <summary>The run's id.</summary>
		public string RunId { get; }

		/// <summary>The platform's status word, e.g. <c>Cancellation requested</c>. Free text — compare loosely or not at all.</summary>
		public string? Status { get; }

		/// <summary>When cancellation was first requested; a repeated cancel keeps this time.</summary>
		public DateTimeOffset? CancelRequestedAt { get; }

		/// <summary>
		/// <see langword="true"/> when the run had already ended (<c>409 run_already_ended</c>): nothing was cancelled and
		/// nothing was written. <see cref="CancelRequestedAt"/> and <see cref="Status"/> are then <see langword="null"/>.
		/// </summary>
		[JsonIgnore]
		public bool AlreadyEnded { get; }

		/// <summary>
		/// For <see cref="AlreadyEnded"/>: the state the run ended in, as the platform reported it; <see langword="null"/> when the
		/// response did not carry one, and <see cref="RunState.Unknown"/> for a state this SDK does not know.
		/// </summary>
		[JsonIgnore]
		public RunState? EndedState { get; }
	}
}
