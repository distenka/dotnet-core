namespace Distenka.Client
{
	/// <summary>
	/// The platform's documented error codes — the <c>code</c> member of an error body — as reported in
	/// <see cref="DistenkaApiException.Code"/>. Compare with <see cref="System.StringComparison.Ordinal"/>. The platform may
	/// add codes; an unknown code is still reported as-is.
	/// </summary>
	/// <remarks>
	/// Verified on 2026-11-01 against the platform's <c>docs/api-run-submission.md</c>, <c>docs/api-invocation.md</c>,
	/// <c>docs/api-run-cancellation.md</c>, <c>docs/api-run-results.md</c> (<c>run_capability_required</c>,
	/// <c>capability_unavailable</c>) and <c>docs/api-callbacks.md</c> (<c>callback_*</c>, <c>callbacks_unavailable</c>).
	/// Recheck on platform upgrades.
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

		/// <summary>
		/// 409 — a <c>Callback-Url</c> was sent, but this deployment has no callback signing key, so it will not promise a
		/// callback it cannot sign. Nothing was started. Invoke without a callback and poll the result instead.
		/// </summary>
		public const string CallbacksUnavailable = "callbacks_unavailable";

		/// <summary>400 — the <c>Callback-Url</c> is not <c>https</c> (plain <c>http</c>, <c>ftp</c>, <c>file</c> or anything else).</summary>
		public const string CallbackNotHttps = "callback_not_https";

		/// <summary>400 — the <c>Callback-Url</c> carries credentials (user info). Verify the callback signature instead.</summary>
		public const string CallbackUserInfo = "callback_userinfo";

		/// <summary>400 — the <c>Callback-Url</c> is longer than 2048 characters.</summary>
		public const string CallbackTooLong = "callback_too_long";

		/// <summary>400 — the <c>Callback-Url</c> is not an absolute URL.</summary>
		public const string CallbackMalformed = "callback_malformed";

		/// <summary>400 — the <c>Callback-Url</c>'s host does not resolve.</summary>
		public const string CallbackUnresolvable = "callback_unresolvable";

		/// <summary>
		/// 400 — the <c>Callback-Url</c>'s host resolves to an address the platform does not open connections to (private,
		/// loopback, link-local, carrier-grade NAT, documentation, multicast — in any IPv4 or IPv6 notation). The error names
		/// the kind of address, never the address.
		/// </summary>
		public const string CallbackPrivateAddress = "callback_private_address";

		/// <summary>503 — the run-result capability service is unavailable. Retry later.</summary>
		public const string CapabilityUnavailable = "capability_unavailable";
	}
}
