namespace Distenka.Client
{
	/// <summary>
	/// The platform's documented error codes — the <c>code</c> member of an error body — as reported in
	/// <see cref="DistenkaApiException.Code"/>. Compare with <see cref="System.StringComparison.Ordinal"/>. The platform may
	/// add codes; an unknown code is still reported as-is.
	/// </summary>
	/// <remarks>
	/// Verified on 2026-11-01 against the platform's <c>docs/api-run-submission.md</c>, <c>docs/api-invocation.md</c> and
	/// <c>docs/api-run-cancellation.md</c>. <c>run_capability_required</c> and <c>capability_unavailable</c> come from
	/// <c>docs/api-run-results.md</c> and <c>callbacks_unavailable</c> from <c>docs/api-callbacks.md</c>, as recorded in the
	/// backlog @ 4c3cf2c. Recheck on platform upgrades.
	/// </remarks>
	public static class DistenkaErrorCodes
	{
		/// <summary>400 — the <c>Idempotency-Key</c> header is not 1–255 printable ASCII characters without spaces.</summary>
		public const string IdempotencyKeyMalformed = "idempotency_key_malformed";

		/// <summary>409 — the <c>Idempotency-Key</c> was already used for a different request. Terminal: do not retry with this key.</summary>
		public const string IdempotencyKeyReuse = "idempotency_key_reuse";

		/// <summary>409 — a request with this <c>Idempotency-Key</c> is still being processed. Retry later with the same key.</summary>
		public const string IdempotencyKeyInFlight = "idempotency_key_in_flight";

		/// <summary>429 — the owner's account already has as many runs waiting to start as its plan allows. Nothing was accepted. Honour <c>Retry-After</c>.</summary>
		public const string QueueDepthExceeded = "queue_depth_exceeded";

		/// <summary>429 — the platform's queue, the owner's share of it, or the platform's database storage is full. Nothing was accepted. Honour <c>Retry-After</c>.</summary>
		public const string PlatformBackpressure = "platform_backpressure";

		/// <summary>429 — the server is accepting as many runs as it can at this moment. Nothing was accepted. Honour <c>Retry-After</c> (about a second).</summary>
		public const string PlatformOverloaded = "platform_overloaded";

		/// <summary>429 — the key's per-minute request ceiling was exceeded. Honour <c>Retry-After</c>.</summary>
		public const string RateLimitExceeded = "rate_limit_exceeded";

		/// <summary>402 — the invocation key has spent the total request allowance its developer set (the body carries <c>limit</c>). Waiting does not help: the developer must raise the allowance or issue another key.</summary>
		public const string QuotaExhausted = "quota_exhausted";

		/// <summary>409 — the run already ended, so it cannot be cancelled.</summary>
		public const string RunAlreadyEnded = "run_already_ended";

		/// <summary>403 — the key is valid but its scope does not allow this operation (e.g. an invocation key without <c>invoke</c>).</summary>
		public const string ScopeViolation = "scope_violation";

		/// <summary>403 — an account credential was presented to the invocation route, which requires an invocation key (<c>DKI-…</c>).</summary>
		public const string InvocationKeyRequired = "invocation_key_required";

		/// <summary>403 — the route requires a run-result capability (<c>drc1.…</c>).</summary>
		public const string RunCapabilityRequired = "run_capability_required";

		/// <summary>409 — the developer's configuration for this processor is incomplete. Nothing the caller can fix by retrying.</summary>
		public const string ProcessorUnavailable = "processor_unavailable";

		/// <summary>Callbacks are not available on this deployment.</summary>
		public const string CallbacksUnavailable = "callbacks_unavailable";

		/// <summary>503 — the run-result capability service is unavailable. Retry later.</summary>
		public const string CapabilityUnavailable = "capability_unavailable";
	}
}
