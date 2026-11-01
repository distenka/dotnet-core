using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Runs
{
	/// <summary>
	/// Operations on runs: submit, read details, cancel. Authenticated with an account key (<c>DK-…</c>).
	/// </summary>
	public interface IRunsClient
	{
		/// <summary>
		/// Submits a run of <paramref name="processorId"/> — <c>POST /api/runs</c>. Returns once the platform has durably
		/// recorded and queued the run (<c>202</c>), not when it starts; read its progress with the run's details.
		/// </summary>
		/// <param name="processorId">The processor to run.</param>
		/// <param name="configuration">
		/// The run configuration, a JSON object; it runs exactly as sent, except for properties the processor's owner has
		/// bound server-side. Never put secrets in it — bind them server-side instead.
		/// </param>
		/// <param name="options">Optional settings, notably <see cref="SubmitRunOptions.IdempotencyKey"/>.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		/// <returns>The run's id; <see cref="RunAccepted.Replayed"/> tells a replayed submission from a new one.</returns>
		/// <exception cref="System.ArgumentException">An empty <paramref name="processorId"/>, a <paramref name="configuration"/> that is not a JSON object, or an empty idempotency key.</exception>
		/// <exception cref="DistenkaApiException">
		/// The platform refused the submission, e.g. <c>404</c> (unknown processor), <c>409 idempotency_key_reuse</c>, or a
		/// <c>429</c> that outlasted the retries (<see cref="DistenkaApiException.RetryAfter"/> says when to try again).
		/// </exception>
		Task<RunAccepted> SubmitAsync(string processorId, JsonElement configuration, SubmitRunOptions? options = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Submits a run whose configuration is <paramref name="configuration"/>, serialized with
		/// <paramref name="configurationTypeInfo"/> (from your own <c>JsonSerializerContext</c>, so no reflection is
		/// needed). See <see cref="SubmitAsync(string, JsonElement, SubmitRunOptions, CancellationToken)"/>.
		/// </summary>
		/// <typeparam name="TConfig">The configuration type; it must serialize to a JSON object.</typeparam>
		/// <param name="processorId">The processor to run.</param>
		/// <param name="configuration">The run configuration.</param>
		/// <param name="configurationTypeInfo">How to serialize <paramref name="configuration"/>.</param>
		/// <param name="options">Optional settings, notably <see cref="SubmitRunOptions.IdempotencyKey"/>.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		Task<RunAccepted> SubmitAsync<TConfig>(string processorId, TConfig configuration, JsonTypeInfo<TConfig> configurationTypeInfo,
			SubmitRunOptions? options = null, CancellationToken cancellationToken = default);
	}
}
