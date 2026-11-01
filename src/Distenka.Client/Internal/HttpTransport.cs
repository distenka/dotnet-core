using Distenka.Client.Authentication;
using Distenka.Client.Resilience;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// The single path every request takes (DCS-020): builds request URIs from <see cref="DistenkaClientOptions.BaseAddress"/>
	/// and adds the <c>User-Agent</c> and <c>Authorization</c> headers. Applying them per request — instead of in a
	/// <see cref="DelegatingHandler"/> — gives a caller-supplied <see cref="HttpClient"/> the same behaviour as the one
	/// created by <c>AddDistenkaClient</c>. Retries (DCS-025): <see cref="SendWithRetryAsync"/>. Timeouts per call: DCS-024.
	/// </summary>
	sealed class HttpTransport
	{
		internal const string SdkProductName = "Distenka.Client";

		/// <summary>The package version without build metadata, e.g. <c>2.0.0-preview.1</c>.</summary>
		internal static readonly string SdkVersion = ResolveSdkVersion();

		readonly IDistenkaCredential? credential;
		readonly string? applicationUserAgent;
		readonly ProductInfoHeaderValue sdkProduct = new ProductInfoHeaderValue(SdkProductName, SdkVersion);

		/// <param name="httpClient">Sends the requests; not owned.</param>
		/// <param name="options">Already validated; values are copied, later changes are not observed.</param>
		internal HttpTransport(HttpClient httpClient, DistenkaClientOptions options)
			: this(httpClient, options, null)
		{
		}

		/// <param name="retryPolicy">Replaces the policy built from <paramref name="options"/> (tests inject the clock and randomness).</param>
		internal HttpTransport(HttpClient httpClient, DistenkaClientOptions options, RetryPolicy? retryPolicy)
		{
			HttpClient = httpClient;
			RetryPolicy = retryPolicy ?? new RetryPolicy(options.MaxRetries, options.MaxRetryDelay);
			BaseAddress = WithTrailingSlash(options.BaseAddress!);
			credential = options.Credential;
			applicationUserAgent = options.UserAgent is string ua && ua.Trim().Length > 0 ? ua.Trim() : null;
		}

		internal HttpClient HttpClient { get; }

		internal RetryPolicy RetryPolicy { get; }

		/// <summary>The configured base address, always ending with <c>/</c> so relative paths append to it.</summary>
		internal Uri BaseAddress { get; }

		/// <summary>Creates a request for <paramref name="relativePath"/> under <see cref="BaseAddress"/> (a leading <c>/</c> is ignored).</summary>
		internal HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
		{
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			if (relativePath == null)
				throw new ArgumentNullException(nameof(relativePath));

			return new HttpRequestMessage(method, new Uri(BaseAddress, relativePath.TrimStart('/')));
		}

		/// <summary>
		/// Sends a request built by <paramref name="createRequest"/> (called once per attempt: a request message cannot be
		/// sent twice), retrying per <see cref="RetryPolicy"/>. Returns a success (2xx) response; any other final outcome
		/// throws a <see cref="DistenkaApiException"/> carrying the last response's status, code and <c>Retry-After</c>.
		/// </summary>
		/// <param name="createRequest">Builds a fresh request, content included, for each attempt.</param>
		/// <param name="completionOption">When the returned task completes.</param>
		/// <param name="cancellationToken">Cancels the request and any wait between attempts.</param>
		/// <param name="replaySafe">
		/// The operation is documented as safe to repeat although it is neither <c>GET</c>/<c>HEAD</c> nor carries an
		/// <c>Idempotency-Key</c> (e.g. cancelling a run: "repeating a cancel is safe").
		/// </param>
		internal async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> createRequest, HttpCompletionOption completionOption,
			CancellationToken cancellationToken, bool replaySafe = false)
		{
			if (createRequest == null)
				throw new ArgumentNullException(nameof(createRequest));

			var previous = RetryPolicy.BaseDelay;
			for (var attempt = 0; ; attempt++)
			{
				using var request = createRequest();
				var safe = replaySafe || RetryPolicy.IsReplaySafe(request);
				var mayRetry = safe && attempt < RetryPolicy.MaxRetries;

				HttpResponseMessage response;
				try
				{
					response = await SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
				}
				catch (HttpRequestException) when (mayRetry && !cancellationToken.IsCancellationRequested)
				{
					// Network failure: the request may or may not have reached the platform, so only a replay-safe one is repeated.
					await RetryPolicy.DelayAsync(RetryPolicy.NextDelay(null, ref previous)!.Value, cancellationToken).ConfigureAwait(false);
					continue;
				}

				if (response.IsSuccessStatusCode)
					return response;

				DistenkaApiException error;
				using (response)
					error = await ApiErrorReader.CreateAsync(response, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

				if (!mayRetry || !error.IsRetryable)
					throw error;

				var wait = RetryPolicy.NextDelay(error.RetryAfter, ref previous);
				if (wait == null)
					throw error; // Retry-After beyond MaxRetryDelay: the caller schedules it from error.RetryAfter.

				await RetryPolicy.DelayAsync(wait.Value, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Whether a success response is an idempotent replay (<c>Idempotency-Replayed: true</c>).</summary>
		internal static bool IsReplayed(HttpResponseMessage response) =>
			response.Headers.TryGetValues("Idempotency-Replayed", out var values) &&
			values.Any(v => string.Equals(v.Trim(), "true", StringComparison.OrdinalIgnoreCase));

		/// <summary>Adds the SDK headers and sends <paramref name="request"/> once (no retry).</summary>
		internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
		{
			await PrepareAsync(request, cancellationToken).ConfigureAwait(false);
			return await HttpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Adds <c>User-Agent: [application] Distenka.Client/&lt;version&gt;</c> and, when a credential is configured and the
		/// request has no <c>Authorization</c> header yet (an operation may set a per-call credential), <c>Authorization: Bearer</c>.
		/// </summary>
		internal async Task PrepareAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			var userAgent = request.Headers.UserAgent;
			if (!userAgent.Any(p => p.Product != null && string.Equals(p.Product.Name, SdkProductName, StringComparison.OrdinalIgnoreCase)))
			{
				if (applicationUserAgent != null && userAgent.Count == 0)
					userAgent.TryParseAdd(applicationUserAgent);

				userAgent.Add(sdkProduct);
			}

			if (credential != null && request.Headers.Authorization == null)
			{
				var token = await credential.GetBearerTokenAsync(cancellationToken).ConfigureAwait(false);
				if (token == null || token.Trim().Length == 0)
					throw new InvalidOperationException($"The configured credential ({credential.GetType().Name}) returned an empty bearer token.");

				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			}
		}

		static Uri WithTrailingSlash(Uri baseAddress)
		{
			var text = baseAddress.AbsoluteUri;
			return text.EndsWith("/", StringComparison.Ordinal) ? baseAddress : new Uri(text + "/", UriKind.Absolute);
		}

		static string ResolveSdkVersion()
		{
			var assembly = typeof(HttpTransport).Assembly;
			var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (version == null || version.Length == 0)
				version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

			var plus = version.IndexOf('+');
			return plus >= 0 ? version.Substring(0, plus) : version;
		}
	}
}
