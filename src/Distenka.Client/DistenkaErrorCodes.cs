namespace Distenka.Client
{
	/// <summary>
	/// The platform's documented error codes — the <c>code</c> member of an error body — as reported in
	/// <see cref="DistenkaApiException.Code"/>. Compare with <see cref="System.StringComparison.Ordinal"/>. The platform may
	/// add codes; an unknown code is still reported as-is.
	/// </summary>
	/// <remarks>
	/// Sourced from the platform's API documentation (<c>docs/api-run-submission.md</c>, <c>docs/api-invocation.md</c>,
	/// <c>docs/api-run-cancellation.md</c>) as recorded in the SDK backlog @ 4c3cf2c. Recheck on platform upgrades.
	/// </remarks>
	public static class DistenkaErrorCodes
	{
		/// <summary>400 — the <c>Idempotency-Key</c> header is not 1–255 printable ASCII characters without spaces.</summary>
		public const string IdempotencyKeyMalformed = "idempotency_key_malformed";

		/// <summary>409 — the <c>Idempotency-Key</c> was already used for a different request. Terminal: do not retry with this key.</summary>
		public const string IdempotencyKeyReuse = "idempotency_key_reuse";

		/// <summary>409 — a request with this <c>Idempotency-Key</c> is still being processed. Retry later with the same key.</summary>
		public const string IdempotencyKeyInFlight = "idempotency_key_in_flight";

		/// <summary>429 — the account's run queue is full. Honour <c>Retry-After</c>.</summary>
		public const string QueueDepthExceeded = "queue_depth_exceeded";

		/// <summary>429 — the platform is applying backpressure. Honour <c>Retry-After</c>.</summary>
		public const string PlatformBackpressure = "platform_backpressure";

		/// <summary>429 — the platform is overloaded. Honour <c>Retry-After</c>.</summary>
		public const string PlatformOverloaded = "platform_overloaded";

		/// <summary>429 — the caller's request rate limit was exceeded. Honour <c>Retry-After</c>.</summary>
		public const string RateLimitExceeded = "rate_limit_exceeded";

		/// <summary>402 — the account's quota is exhausted. Terminal until the quota changes.</summary>
		public const string QuotaExhausted = "quota_exhausted";

		/// <summary>409 — the run already ended, so it cannot be cancelled.</summary>
		public const string RunAlreadyEnded = "run_already_ended";

		/// <summary>403 — the credential's scope does not allow this operation.</summary>
		public const string ScopeViolation = "scope_violation";

		/// <summary>403 — the route requires an invocation key (<c>DKI-…</c>).</summary>
		public const string InvocationKeyRequired = "invocation_key_required";

		/// <summary>403 — the route requires a run-result capability (<c>drc1.…</c>).</summary>
		public const string RunCapabilityRequired = "run_capability_required";

		/// <summary>409 — the processor or plugin cannot be run at the moment.</summary>
		public const string ProcessorUnavailable = "processor_unavailable";

		/// <summary>Callbacks are not available on this deployment.</summary>
		public const string CallbacksUnavailable = "callbacks_unavailable";

		/// <summary>503 — the run-result capability service is unavailable. Retry later.</summary>
		public const string CapabilityUnavailable = "capability_unavailable";
	}
}
