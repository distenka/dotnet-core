using Distenka.Client.Runs;
using Distenka.Client.Serialization;
using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Invocations
{
	/// <summary>
	/// The result of one invoked run, read with its run-result capability — <c>GET /api/runs/result</c>
	/// (<c>docs/api-run-results.md</c> §The response). Six fields, and the list is closed: the document never carries the
	/// owner's configuration, plugin, infrastructure or any other run.
	/// </summary>
	public sealed class RunResult
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunResult(string runId, string? status, int? progress, string? output, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
		{
			RunId = runId;
			Status = status;
			Progress = progress;
			Output = output;
			StartedAt = startedAt;
			CompletedAt = completedAt;
		}

		/// <summary>The run's id.</summary>
		public string RunId { get; }

		/// <summary>The run's status as sent, e.g. <c>Running</c> or <c>Completed</c>. Parsed in <see cref="State"/>.</summary>
		public string? Status { get; }

		/// <summary>Progress in percent (<c>0</c>–<c>100</c>), when reported.</summary>
		public int? Progress { get; }

		/// <summary>What the job printed (stdout, at most 2000 lines); <see langword="null"/> until there is output.</summary>
		public string? Output { get; }

		/// <summary>When the run started; <see langword="null"/> while it waits to start.</summary>
		public DateTimeOffset? StartedAt { get; }

		/// <summary>When the run ended; <see langword="null"/> while it is not finished.</summary>
		public DateTimeOffset? CompletedAt { get; }

		/// <summary>
		/// <see cref="Status"/> as a <see cref="RunState"/>; <see cref="RunState.Unknown"/> for a word the SDK does not know.
		/// The run is finished when <see cref="RunStateExtensions.IsTerminal"/> is <see langword="true"/>.
		/// </summary>
		[JsonIgnore]
		public RunState State => RunStateNames.Parse(Status);
	}
}
