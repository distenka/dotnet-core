using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The body of <c>POST /api/runs/{runId}/cancel</c> — <c>200 OK</c> means the cancellation request stands
	/// (<c>docs/api-run-cancellation.md</c>). The same body is returned for a repeated cancel; read the run's details to see
	/// its state.
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

		/// <summary>The run's id.</summary>
		public string RunId { get; }

		/// <summary>The platform's status word, e.g. <c>Cancellation requested</c>. Free text — compare loosely or not at all.</summary>
		public string? Status { get; }

		/// <summary>When cancellation was first requested; a repeated cancel keeps this time.</summary>
		public DateTimeOffset? CancelRequestedAt { get; }
	}
}
