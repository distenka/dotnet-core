using Distenka.Client.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>DCS-020: every request carries <c>User-Agent: Distenka.Client/&lt;version&gt;</c> and the configured bearer credential.</summary>
	public class Request_headers
	{
		const string Token = "DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

		static string ExpectedVersion()
		{
			var informational = typeof(DistenkaClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			return informational.Split('+')[0];
		}

		static (DistenkaClient client, RecordingHandler handler) ViaDependencyInjection(Action<DistenkaClientOptions> configure)
		{
			var handler = new RecordingHandler();
			var provider = new ServiceCollection()
				.AddDistenkaClient(configure)
				.ConfigurePrimaryHttpMessageHandler(() => handler)
				.Services.BuildServiceProvider();

			return ((DistenkaClient)provider.GetRequiredService<IDistenkaClient>(), handler);
		}

		static (DistenkaClient client, RecordingHandler handler) ViaConstructor(DistenkaClientOptions options)
		{
			var handler = new RecordingHandler();
			return (new DistenkaClient(new HttpClient(handler), options), handler);
		}

		static async Task<HttpRequestMessage> SendOne(DistenkaClient client, RecordingHandler handler, string path = "api/runs", CancellationToken cancellationToken = default)
		{
			using var request = client.Transport.CreateRequest(HttpMethod.Get, path);
			using var response = await client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			return handler.Requests.Single();
		}

		[Fact]
		public async Task user_agent_and_bearer_token_are_sent_through_the_DI_pipeline()
		{
			var (client, handler) = ViaDependencyInjection(o =>
			{
				o.BaseAddress = new Uri("https://api.example");
				o.Credential = new FixedTokenCredential(Token);
			});

			var sent = await SendOne(client, handler);

			sent.Headers.UserAgent.Should().ContainSingle(p => p.Product != null && p.Product.Name == "Distenka.Client")
				.Which.Product.Version.Should().Be(ExpectedVersion());
			sent.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", Token));
		}

		[Fact]
		public async Task the_constructor_path_sends_the_same_headers()
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example"),
				Credential = new FixedTokenCredential(Token),
			});

			var sent = await SendOne(client, handler);

			sent.Headers.UserAgent.Select(p => p.ToString()).Should().Equal($"Distenka.Client/{ExpectedVersion()}");
			sent.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", Token));
		}

		[Fact]
		public async Task every_request_gets_the_headers_and_the_version_has_no_build_metadata()
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = new FixedTokenCredential(Token) });

			for (var i = 0; i < 3; i++)
			{
				using var request = client.Transport.CreateRequest(HttpMethod.Post, "api/runs");
				using var _ = await client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
			}

			handler.Requests.Should().HaveCount(3).And.OnlyContain(r =>
				r.Headers.UserAgent.Count(p => p.Product != null && p.Product.Name == "Distenka.Client") == 1 &&
				r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Bearer");
			HttpTransport.SdkVersion.Should().NotContain("+");
		}

		[Fact]
		public async Task no_other_authentication_header_is_sent()
		{
			// ADR-006: one Authorization: Bearer header; never X-Product-Key or x-api-key.
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = new FixedTokenCredential(Token) });

			var sent = await SendOne(client, handler);

			sent.Headers.Select(h => h.Key).Should().NotContain(k =>
				string.Equals(k, "X-Product-Key", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "x-api-key", StringComparison.OrdinalIgnoreCase));
			sent.Headers.Where(h => h.Value.Any(v => v.Contains(Token))).Select(h => h.Key).Should().Equal("Authorization");
		}

		[Fact]
		public async Task application_user_agent_comes_first()
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), UserAgent = "MyApp/1.2" });

			var sent = await SendOne(client, handler);

			sent.Headers.UserAgent.Select(p => p.ToString()).Should().Equal("MyApp/1.2", $"Distenka.Client/{ExpectedVersion()}");
		}

		[Fact]
		public async Task without_a_credential_no_authorization_header_is_sent()
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example") });

			var sent = await SendOne(client, handler);

			sent.Headers.Authorization.Should().BeNull();
		}

		[Fact]
		public async Task a_per_request_authorization_header_is_kept()
		{
			var credential = new FixedTokenCredential(Token);
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = credential });

			using var request = client.Transport.CreateRequest(HttpMethod.Get, "api/runs/result");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "drc1.opaque");
			using var _ = await client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

			handler.Requests.Single().Headers.Authorization.Parameter.Should().Be("drc1.opaque");
			credential.Calls.Should().Be(0);
		}

		[Fact]
		public async Task the_cancellation_token_reaches_the_credential()
		{
			var credential = new FixedTokenCredential(Token);
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = credential });
			using var cts = new CancellationTokenSource();

			await SendOne(client, handler, cancellationToken: cts.Token);

			credential.LastCancellationToken.Should().Be(cts.Token);
		}

		[Fact]
		public async Task an_empty_token_fails_without_sending()
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = new FixedTokenCredential(" ") });

			using var request = client.Transport.CreateRequest(HttpMethod.Get, "api/runs");
			var send = () => client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

			await send.Should().ThrowAsync<InvalidOperationException>();
			handler.Requests.Should().BeEmpty();
		}

		[Theory]
		[InlineData("https://api.example", "api/runs", "https://api.example/api/runs")]
		[InlineData("https://api.example/", "/api/runs", "https://api.example/api/runs")]
		[InlineData("https://api.example/platform", "api/runs", "https://api.example/platform/api/runs")]
		[InlineData("https://api.example:8443/platform/", "api/runs/abc/details", "https://api.example:8443/platform/api/runs/abc/details")]
		public async Task request_uris_are_relative_to_the_base_address_path(string baseAddress, string path, string expected)
		{
			var (client, handler) = ViaConstructor(new DistenkaClientOptions { BaseAddress = new Uri(baseAddress) });

			var sent = await SendOne(client, handler, path);

			sent.RequestUri.Should().Be(new Uri(expected));
		}

		[Fact]
		public void options_changed_after_construction_are_not_observed()
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/") };
			var (client, _) = ViaConstructor(options);

			options.BaseAddress = new Uri("https://other.example/");

			client.Transport.BaseAddress.Should().Be(new Uri("https://api.example/"));
		}
	}
}
