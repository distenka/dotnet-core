namespace Distenka.Client.Runs
{
	/// <summary>Options for <see cref="IRunsClient.SubmitAsync"/>.</summary>
	public sealed class SubmitRunOptions
	{
		/// <summary>
		/// Sent as the <c>Idempotency-Key</c> header: at most one run is started for your account and this key within
		/// 24 hours, and a repeat of the same request returns the first run (<see cref="RunAccepted.Replayed"/>). It also lets
		/// the client retry the submission safely after a transient failure — without a key a submission is never retried,
		/// because each request could start a run. 1–255 printable ASCII characters, no spaces; any correlator you already
		/// have (an order number, a batch id, a UUID). Reusing a key for a <em>different</em> request is a
		/// <c>409 idempotency_key_reuse</c>.
		/// </summary>
		public string? IdempotencyKey { get; set; }
	}
}
