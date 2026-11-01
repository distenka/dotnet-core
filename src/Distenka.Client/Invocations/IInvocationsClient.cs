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
	}
}
