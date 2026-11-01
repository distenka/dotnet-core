using Distenka.Client.Internal;
using Distenka.Client.Invocations;
using Distenka.Client.Runs;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;

namespace Distenka.Client
{
	/// <summary>
	/// The default <see cref="IDistenkaClient"/>. With dependency injection, register it with
	/// <c>services.AddDistenkaClient(…)</c> and resolve <see cref="IDistenkaClient"/>; without it, construct one over
	/// a long-lived <see cref="HttpClient"/>.
	/// </summary>
	public sealed class DistenkaClient : IDistenkaClient
	{
		/// <summary>
		/// Creates a client that sends its requests through <paramref name="httpClient"/>. The client does not dispose
		/// <paramref name="httpClient"/> and does not change its settings: the base address, credential and user agent
		/// come from <paramref name="options"/> and are applied to each request.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to send requests with; reuse one for the application's lifetime.</param>
		/// <param name="options">The client settings; validated here. Later changes to the object are not observed.</param>
		/// <exception cref="ArgumentNullException"><paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
		/// <exception cref="OptionsValidationException"><paramref name="options"/> is invalid (for example, no <see cref="DistenkaClientOptions.BaseAddress"/>).</exception>
		public DistenkaClient(HttpClient httpClient, DistenkaClientOptions options)
			: this(new HttpTransport(httpClient ?? throw new ArgumentNullException(nameof(httpClient)), Validate(options)))
		{
		}

		internal DistenkaClient(HttpTransport transport)
		{
			Transport = transport;
			Runs = new RunsClient(transport);
			Invocations = new InvocationsClient(transport);
		}

		/// <inheritdoc/>
		public IRunsClient Runs { get; }

		/// <inheritdoc/>
		public IInvocationsClient Invocations { get; }

		internal HttpTransport Transport { get; }

		static DistenkaClientOptions Validate(DistenkaClientOptions options)
		{
			if (options == null)
				throw new ArgumentNullException(nameof(options));

			var result = new DistenkaClientOptionsValidator().Validate(Options.DefaultName, options);
			if (result.Failed)
				throw new OptionsValidationException(Options.DefaultName, typeof(DistenkaClientOptions), result.Failures);

			return options;
		}
	}
}
