using Distenka.Client.Authentication;
using Distenka.Client.Serialization;
using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Invocations
{
	/// <summary>
	/// The body of <c>POST /v1/invoke/{pluginId}</c> — <c>202 Accepted</c>, or <c>200</c> for an idempotent replay
	/// (<c>docs/api-invocation.md</c> §The response). Async only: read the result with <see cref="ResultCapability"/>.
	/// </summary>
	public sealed class InvocationAccepted
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public InvocationAccepted(string runId, string? status, RunCapabilityCredential? resultCapability, DateTimeOffset? resultCapabilityExpiresAt)
		{
			RunId = runId;
			Status = status;
			ResultCapability = resultCapability;
			ResultCapabilityExpiresAt = resultCapabilityExpiresAt;
		}

		/// <summary>The run's id.</summary>
		public string RunId { get; }

		/// <summary>The platform's status word, e.g. <c>Pending</c>. Free text.</summary>
		public string? Status { get; }

		/// <summary>
		/// The capability for reading this run's result (<c>drc1.…</c>, good for 24 hours). <see langword="null"/> when the
		/// deployment has no capability signing key: the run was accepted, but its result cannot be read through the API —
		/// tell the processor's developer.
		/// </summary>
		[JsonConverter(typeof(RunCapabilityCredentialJsonConverter))]
		public RunCapabilityCredential? ResultCapability { get; }

		/// <summary>When <see cref="ResultCapability"/> expires.</summary>
		public DateTimeOffset? ResultCapabilityExpiresAt { get; }
	}
}
