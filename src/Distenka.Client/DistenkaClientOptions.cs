using Distenka.Client.Authentication;
using System;

namespace Distenka.Client
{
	/// <summary>
	/// Settings for a <see cref="DistenkaClient"/>. Bind them from configuration with
	/// <c>services.AddDistenkaClient(configuration.GetSection("Distenka"))</c> or set them in code with
	/// <c>services.AddDistenkaClient(o =&gt; …)</c>; they are validated when the application starts.
	/// </summary>
	/// <remarks>
	/// There is no default host: every deployment is self-hosted or on-premises and supplies its own
	/// <see cref="BaseAddress"/> (AD-4). Never put a credential's secret in source code or in a committed
	/// configuration file; supply it from an environment variable or a secret store.
	/// </remarks>
	public sealed class DistenkaClientOptions
	{
		/// <summary>
		/// The base URL of your Distenka deployment's API, for example <c>https://api.example.com/</c>. Required.
		/// Must be an absolute <c>https</c> URL (or <c>http</c> when <see cref="AllowInsecureHttp"/> is set) without
		/// user information, query or fragment. A path is allowed: requests are sent relative to it.
		/// </summary>
		public Uri? BaseAddress { get; set; }

		/// <summary>
		/// The credential sent as <c>Authorization: Bearer …</c> on every request that does not already carry an
		/// <c>Authorization</c> header. When <see langword="null"/>, requests are sent without one and the platform
		/// answers protected routes with 401.
		/// </summary>
		public IDistenkaCredential? Credential { get; set; }

		/// <summary>
		/// The time allowed for one request, from sending to the end of the response headers. Default 100 seconds.
		/// Must be positive, or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>. Applied to the
		/// <see cref="System.Net.Http.HttpClient"/> created by <c>AddDistenkaClient</c>; a caller-supplied
		/// <see cref="System.Net.Http.HttpClient"/> keeps its own timeout.
		/// </summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

		/// <summary>
		/// Allows a plain <c>http</c> <see cref="BaseAddress"/>. Off by default: credentials would travel unencrypted.
		/// Use only for a local development deployment.
		/// </summary>
		public bool AllowInsecureHttp { get; set; }

		/// <summary>
		/// Optional product token(s) identifying your application, e.g. <c>MyApp/1.2</c>. Sent in the
		/// <c>User-Agent</c> header before <c>Distenka.Client/&lt;version&gt;</c>, which is always sent.
		/// </summary>
		public string? UserAgent { get; set; }

		/// <summary>
		/// How many times a failed request is retried. Default 3; 0 disables retries; at most 10.
		/// Only requests that are safe to repeat are retried — <c>GET</c>/<c>HEAD</c>, a request carrying an
		/// <c>Idempotency-Key</c>, or an operation the platform documents as idempotent — and only after a retryable
		/// failure (<see cref="DistenkaApiException.IsRetryable"/>, or a network error). A <c>POST</c> without an
		/// <c>Idempotency-Key</c> is never repeated: it could start a second run (AD-6).
		/// </summary>
		public int MaxRetries { get; set; } = 3;

		/// <summary>
		/// The longest the client waits before one retry. Default 60 seconds (one rate-limit window). When the platform's
		/// <c>Retry-After</c> asks for longer, the client does not retry and the <see cref="DistenkaApiException"/> carries
		/// <see cref="DistenkaApiException.RetryAfter"/> for the caller to schedule. Must be positive and at most 10 minutes.
		/// </summary>
		public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);
	}
}
