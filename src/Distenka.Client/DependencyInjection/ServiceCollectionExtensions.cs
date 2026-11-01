using Distenka.Client;
using Distenka.Client.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>Registers the Distenka platform client.</summary>
	public static class DistenkaClientServiceCollectionExtensions
	{
		internal const string HttpClientName = "Distenka.Client";

		/// <summary>
		/// Registers <see cref="IDistenkaClient"/> over <see cref="IHttpClientFactory"/>, with options set in code.
		/// The options are validated when the host starts (a missing <see cref="DistenkaClientOptions.BaseAddress"/> throws
		/// <see cref="OptionsValidationException"/>), or when <see cref="IDistenkaClient"/> is first resolved without a host.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="configure">Sets the options. Take the credential's secret from configuration or a secret store, never a literal.</param>
		/// <returns>The builder of the underlying named <see cref="HttpClient"/>, e.g. to configure its primary handler.</returns>
		public static IHttpClientBuilder AddDistenkaClient(this IServiceCollection services, Action<DistenkaClientOptions> configure)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));
			if (configure == null)
				throw new ArgumentNullException(nameof(configure));

			services.AddOptions<DistenkaClientOptions>().Configure(configure).ValidateOnStart();
			return AddCore(services);
		}

		/// <summary>
		/// Registers <see cref="IDistenkaClient"/> over <see cref="IHttpClientFactory"/>, with options read from a
		/// configuration section (keys <c>BaseAddress</c>, <c>Timeout</c>, <c>AllowInsecureHttp</c>, <c>UserAgent</c>,
		/// <c>MaxRetries</c>, <c>MaxRetryDelay</c>, <c>ApiKey</c>),
		/// e.g. <c>services.AddDistenkaClient(configuration.GetSection("Distenka"))</c>. Validated when the host starts.
		/// </summary>
		/// <remarks>
		/// Supply <c>ApiKey</c> (an account key, <c>DK-…</c>) from a secret source — the environment variable
		/// <c>Distenka__ApiKey</c>, user secrets in Development, or your vault's configuration provider — never from a committed
		/// file or a literal. It becomes an <see cref="Distenka.Client.Authentication.AccountKeyCredential"/>.
		/// </remarks>
		/// <param name="services">The service collection.</param>
		/// <param name="configuration">The configuration section holding the client settings.</param>
		/// <returns>The builder of the underlying named <see cref="HttpClient"/>.</returns>
		public static IHttpClientBuilder AddDistenkaClient(this IServiceCollection services, IConfiguration configuration)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));

			services.AddOptions<DistenkaClientOptions>().Configure(o => ClientConfigurationReader.Apply(configuration, o)).ValidateOnStart();
			return AddCore(services);
		}

		static IHttpClientBuilder AddCore(IServiceCollection services)
		{
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DistenkaClientOptions>, DistenkaClientOptionsValidator>());

			// Transient over the factory's pooled handlers; DistenkaClient owns nothing that needs disposing.
			services.TryAddTransient<IDistenkaClient>(sp =>
			{
				var options = sp.GetRequiredService<IOptions<DistenkaClientOptions>>().Value; // validates
				var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
				return new DistenkaClient(new HttpTransport(httpClient, options));
			});

			// The factory's logging handler writes request headers at Trace level; the bearer token must never be among them
			// (DCS-027, AD-9). Redacted values are logged as "*".
			return services.AddHttpClient(HttpClientName, (sp, httpClient) =>
					httpClient.Timeout = sp.GetRequiredService<IOptions<DistenkaClientOptions>>().Value.Timeout)
				.RedactLoggedHeaders(new[] { "Authorization" });
		}
	}
}
