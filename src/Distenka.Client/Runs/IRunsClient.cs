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

		/// <summary>
		/// Reads one of your runs — <c>GET /api/runs/{runId}/details</c>: its state, lifecycle timestamps, and its execution
		/// log (<see cref="RunDetails.Timeline"/>, <see cref="RunDetails.Output"/>). This is the only way to read a run's
		/// logs: there is no logs endpoint (AD-13).
		/// </summary>
		/// <param name="runId">The run's id, from <see cref="RunAccepted.RunId"/>.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		/// <exception cref="System.ArgumentException"><paramref name="runId"/> is empty.</exception>
		/// <exception cref="DistenkaApiException">
		/// <c>404</c> when the run does not exist, belongs to another account, or has been purged — the platform does not
		/// distinguish them (use <see cref="TryGetDetailsAsync"/> to get <see langword="null"/> instead).
		/// </exception>
		Task<RunDetails> GetDetailsAsync(string runId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Like <see cref="GetDetailsAsync"/>, but returns <see langword="null"/> for a <c>404</c> — convenient in polling loops,
		/// where a run that is not (or no longer) visible is an expected outcome. Every other failure still throws.
		/// </summary>
		/// <param name="runId">The run's id.</param>
		/// <param name="cancellationToken">Cancels the request.</param>
		Task<RunDetails?> TryGetDetailsAsync(string runId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asks the platform to cancel one of your runs — <c>POST /api/runs/{runId}/cancel</c> — at whatever point it has
		/// reached. The request is recorded before the platform answers; a queued run is cancelled at once, a running one by
		/// its executor shortly after, so read the run's details to see when it is <see cref="RunState.Cancelled"/>.
		/// Repeating a cancel is safe and returns the first request's time.
		/// </summary>
		/// <param name="runId">The run's id.</param>
		/// <param name="options">Optional settings (<see cref="CancelRunOptions.ThrowOnAlreadyEnded"/>).</param>
		/// <param name="cancellationToken">Cancels this HTTP request — not the run.</param>
		/// <returns>
		/// The cancellation request; <see cref="CancelResult.AlreadyEnded"/> is <see langword="true"/> (and
		/// <see cref="CancelResult.EndedState"/> set when reported) if the run had already ended.
		/// </returns>
		/// <exception cref="System.ArgumentException"><paramref name="runId"/> is empty.</exception>
		/// <exception cref="DistenkaApiException"><c>404</c> for a run that is not yours, does not exist or was purged.</exception>
		Task<CancelResult> CancelAsync(string runId, CancelRunOptions? options = null, CancellationToken cancellationToken = default);
	}
}
