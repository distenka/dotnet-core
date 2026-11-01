using Distenka.Client.Invocations;
using Distenka.Client.Runs;

namespace Distenka.Client
{
	/// <summary>
	/// Entry point to the Distenka platform's public REST API. Operations are grouped by area; each area is a
	/// sub-client so the API can grow without changing this interface.
	/// </summary>
	/// <remarks>
	/// The client covers runs and invocations only. It has no account, login, token-refresh or key-management
	/// operations: keys are issued and managed in the console (ADR-006).
	/// </remarks>
	public interface IDistenkaClient
	{
		/// <summary>Submitting, inspecting and cancelling runs (account key, <c>DK-…</c>).</summary>
		IRunsClient Runs { get; }

		/// <summary>Invoking published plugins (invocation key, <c>DKI-…</c>).</summary>
		IInvocationsClient Invocations { get; }
	}
}
