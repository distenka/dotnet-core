using Distenka.Client.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Invocations
{
	/// <summary>
	/// Invoking published plugins. Authenticated with an invocation key (<c>DKI-…</c>): configure the client with an
	/// <see cref="Authentication.InvocationKeyCredential"/>. An account key gets <c>403 invocation_key_required</c>.
	/// </summary>
	public interface IInvocationsClient
	{
		/// <summary>
		/// Starts one run of the processor your invocation key was issued for — <c>POST /v1/invoke/{pluginId}</c>. Async only:
		/// returns once the platform has durably queued the run (<c>202</c>), with a capability for reading its result.
		/// </summary>
		/// <param name="pluginId">The processor your key was issued for; any other id is a <c>404</c>.</param>
		/// <param name="input">
		/// The input, a JSON object matching the processor's published schema. Properties the schema does not declare are
		/// rejected (<c>400</c>), not ignored. Send data only — never credentials or endpoints.
		/// </param>
		/// <param name="options">Optional <see cref="InvokeOptions.IdempotencyKey"/> and <see cref="InvokeOptions.CallbackUrl"/>.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		/// <returns>
		/// The run id and <see cref="InvocationAccepted.ResultCapability"/> — which is <see langword="null"/> when the deployment
		/// has no capability signing key (the run was still accepted).
		/// </returns>
		/// <exception cref="System.ArgumentException">An empty <paramref name="pluginId"/>, <paramref name="input"/> that is not a JSON object, an empty idempotency key or a relative callback URL.</exception>
		/// <exception cref="DistenkaApiException">
		/// The platform refused the invocation: <c>400</c> (input does not match the contract), <c>402 quota_exhausted</c>
		/// (allowance spent — <see cref="DistenkaApiException.Limit"/>; waiting does not help), <c>403</c>, <c>404</c>,
		/// <c>409 processor_unavailable</c>, <c>413</c> (over 128 KB), or a <c>429</c> that outlasted the retries.
		/// </exception>
		Task<InvocationAccepted> InvokeAsync(string pluginId, JsonElement input, InvokeOptions? options = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Invokes with <paramref name="input"/> serialized by <paramref name="inputTypeInfo"/> (from your own
		/// <c>JsonSerializerContext</c>). See <see cref="InvokeAsync(string, JsonElement, InvokeOptions, CancellationToken)"/>.
		/// </summary>
		/// <typeparam name="TInput">The input type; it must serialize to a JSON object.</typeparam>
		/// <param name="pluginId">The processor your key was issued for.</param>
		/// <param name="input">The input.</param>
		/// <param name="inputTypeInfo">How to serialize <paramref name="input"/>.</param>
		/// <param name="options">Optional settings.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		Task<InvocationAccepted> InvokeAsync<TInput>(string pluginId, TInput input, JsonTypeInfo<TInput> inputTypeInfo,
			InvokeOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads the result of the run <paramref name="capability"/> was issued for — <c>GET /api/runs/result</c>. The capability
		/// is sent as the bearer token instead of the configured credential; it names the run, so there is no run id to pass.
		/// </summary>
		/// <param name="capability">The <see cref="InvocationAccepted.ResultCapability"/> returned by the invocation.</param>
		/// <param name="etag">
		/// The <see cref="RunResultResponse.ETag"/> of the document you already have, or <see langword="null"/>. When the run has
		/// not changed the answer is <see cref="RunResultResponse.NotModified"/> with no body — cheaper for both sides.
		/// </param>
		/// <param name="cancellationToken">Cancels the request.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="capability"/> is <see langword="null"/>.</exception>
		/// <exception cref="DistenkaApiException">
		/// <c>401</c> — the capability is invalid or expired, or the invocation key behind it was revoked (one answer for all);
		/// <c>403 scope_violation</c> (the key lacks <c>run:read</c>) or <c>run_capability_required</c>; <c>404</c> — the run
		/// is not there yet (archival lag; read again later); or a <c>429</c>/<c>503</c> that outlasted the retries.
		/// </exception>
		Task<RunResultResponse> GetResultAsync(RunCapabilityCredential capability, string? etag = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Polls the result until the run has finished (<c>Completed</c>, <c>Failed</c>, <c>Cancelled</c>, <c>TimedOut</c> or
		/// <c>Expired</c>) and returns the final document. Sends the last <c>ETag</c> on every poll, backs off geometrically,
		/// honours <c>Retry-After</c>, treats <c>404</c> as "not there yet", and stays within
		/// <see cref="PollingOptions.MaxPollsPerMinute"/>. Prefer a callback (<see cref="InvokeOptions.CallbackUrl"/>) where you can.
		/// </summary>
		/// <param name="capability">The <see cref="InvocationAccepted.ResultCapability"/>; polling stops at its <see cref="RunCapabilityCredential.ExpiresAt"/>.</param>
		/// <param name="options">Intervals, timeout and request budget; defaults when <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the wait.</param>
		/// <exception cref="System.ArgumentException"><paramref name="options"/> holds an invalid value.</exception>
		/// <exception cref="System.TimeoutException">The run had not finished by <see cref="PollingOptions.Timeout"/> or the capability's expiry.</exception>
		/// <exception cref="DistenkaApiException">A terminal refusal, e.g. <c>401</c> (expired or revoked) or <c>403</c>; never retried.</exception>
		Task<RunResult> WaitForResultAsync(RunCapabilityCredential capability, PollingOptions? options = null, CancellationToken cancellationToken = default);
	}
}
