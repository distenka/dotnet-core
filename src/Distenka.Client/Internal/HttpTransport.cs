using Distenka.Client.Authentication;
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
	/// created by <c>AddDistenkaClient</c>. Timeouts per call: DCS-024; retries: DCS-025.
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
		{
			HttpClient = httpClient;
			BaseAddress = WithTrailingSlash(options.BaseAddress!);
			credential = options.Credential;
			applicationUserAgent = options.UserAgent is string ua && ua.Trim().Length > 0 ? ua.Trim() : null;
		}

		internal HttpClient HttpClient { get; }

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

		/// <summary>Adds the SDK headers and sends <paramref name="request"/>.</summary>
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
