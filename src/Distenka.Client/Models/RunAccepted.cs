using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// The body of <c>POST /api/runs</c> — <c>202 Accepted</c>, or <c>200</c> for an idempotent replay
	/// (<c>docs/api-run-submission.md</c> §The responses). A replay carries the <em>first</em> run's id.
	/// </summary>
	public sealed class RunAccepted
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunAccepted(string runId, string? processorId, string? trackingUrl)
		{
			RunId = runId;
			ProcessorId = processorId;
			TrackingUrl = trackingUrl;
		}

		/// <summary>The run's id: read it back with the run's details.</summary>
		public string RunId { get; }

		/// <summary>The processor that was submitted.</summary>
		public string? ProcessorId { get; }

		/// <summary>The platform's tracking link for the run, as sent.</summary>
		public string? TrackingUrl { get; }
	}
}
