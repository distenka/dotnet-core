using Distenka.Client.Internal;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>DCS-020: <c>AddDistenkaClient</c> registration and options validation.</summary>
	public class Registration
	{
		const string Secret = "DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

		[Fact]
		public void resolves_IDistenkaClient_with_a_base_address_and_a_credential()
		{
			// AC 1. The literal AccountKeyCredential of the AC is re-homed to DCS-021 (the type is created there).
			using var provider = new ServiceCollection()
				.AddDistenkaClient(o =>
				{
					o.BaseAddress = new Uri("https://api.example");
					o.Credential = new FixedTokenCredential(Secret);
				})
				.Services.BuildServiceProvider();

			var client = provider.GetRequiredService<IDistenkaClient>();

			client.Should().BeOfType<DistenkaClient>();
			client.Runs.Should().NotBeNull();
			client.Invocations.Should().NotBeNull();
		}

		[Fact]
		public async Task missing_base_address_fails_when_the_host_starts()
		{
			// AC 2: OptionsValidationException at startup, not at the first call (ValidateOnStart).
			using var host = new HostBuilder()
				.ConfigureServices(s => s.AddDistenkaClient(o => o.Credential = new FixedTokenCredential(Secret)))
				.Build();

			var start = () => host.StartAsync();

			(await start.Should().ThrowAsync<OptionsValidationException>())
				.Which.Failures.Should().ContainSingle(f => f.Contains("BaseAddress is required"));
		}

		[Fact]
		public void missing_base_address_fails_when_the_client_is_resolved_without_a_host()
		{
			using var provider = new ServiceCollection().AddDistenkaClient(o => { }).Services.BuildServiceProvider();

			var resolve = () => provider.GetRequiredService<IDistenkaClient>();

			resolve.Should().Throw<OptionsValidationException>();
		}

		[Fact]
		public async Task a_valid_configuration_starts_the_host()
		{
			using var host = new HostBuilder()
				.ConfigureServices(s => s.AddDistenkaClient(o => o.BaseAddress = new Uri("https://api.example")))
				.Build();

			await host.StartAsync();
			host.Services.GetRequiredService<IDistenkaClient>().Should().NotBeNull();
			await host.StopAsync();
		}

		[Theory]
		[InlineData("http://api.example/", false, "uses http")]
		[InlineData("ftp://api.example/", false, "must use https")]
		[InlineData("https://user:pw@api.example/", false, "user information")]
		[InlineData("https://api.example/?x=1", false, "query or fragment")]
		[InlineData("https://api.example/#top", false, "query or fragment")]
		public void invalid_base_addresses_are_rejected(string url, bool allowInsecureHttp, string expected)
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri(url), AllowInsecureHttp = allowInsecureHttp, Credential = new FixedTokenCredential(Secret) };

			var result = new DistenkaClientOptionsValidator().Validate(Options.DefaultName, options);

			result.Failed.Should().BeTrue();
			result.Failures.Should().Contain(f => f.Contains(expected));
			result.Failures.Should().NotContain(f => f.Contains(Secret), "no message may include the credential");
		}

		[Fact]
		public void relative_base_address_is_rejected()
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("api/", UriKind.Relative) };

			new DistenkaClientOptionsValidator().Validate(Options.DefaultName, options)
				.Failures.Should().ContainSingle(f => f.Contains("absolute URL"));
		}

		[Theory]
		[InlineData("https://api.example/")]
		[InlineData("https://api.example:8443/platform/")]
		public void https_base_addresses_are_accepted(string url) =>
			new DistenkaClientOptionsValidator().Validate(Options.DefaultName, new DistenkaClientOptions { BaseAddress = new Uri(url) })
				.Succeeded.Should().BeTrue();

		[Fact]
		public void http_is_accepted_only_when_explicitly_allowed()
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("http://localhost:5000/"), AllowInsecureHttp = true };

			new DistenkaClientOptionsValidator().Validate(Options.DefaultName, options).Succeeded.Should().BeTrue();
		}

		[Fact]
		public void timeout_must_be_positive_or_infinite()
		{
			var validator = new DistenkaClientOptionsValidator();
			var baseAddress = new Uri("https://api.example/");

			validator.Validate(null, new DistenkaClientOptions { BaseAddress = baseAddress, Timeout = TimeSpan.Zero }).Failed.Should().BeTrue();
			validator.Validate(null, new DistenkaClientOptions { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(-1) }).Failed.Should().BeTrue();
			validator.Validate(null, new DistenkaClientOptions { BaseAddress = baseAddress, Timeout = Timeout.InfiniteTimeSpan }).Succeeded.Should().BeTrue();
			new DistenkaClientOptions().Timeout.Should().Be(TimeSpan.FromSeconds(100));
		}

		[Fact]
		public void invalid_user_agent_is_rejected()
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/"), UserAgent = "@app" };

			new DistenkaClientOptionsValidator().Validate(null, options).Failures.Should().ContainSingle(f => f.Contains("UserAgent"));
		}

		[Fact]
		public void the_http_client_timeout_follows_the_options()
		{
			using var provider = new ServiceCollection()
				.AddDistenkaClient(o => { o.BaseAddress = new Uri("https://api.example"); o.Timeout = TimeSpan.FromSeconds(7); })
				.Services.BuildServiceProvider();

			var client = (DistenkaClient)provider.GetRequiredService<IDistenkaClient>();

			client.Transport.HttpClient.Timeout.Should().Be(TimeSpan.FromSeconds(7));
		}

		[Fact]
		public void reads_options_from_a_configuration_section()
		{
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
			{
				["Distenka:BaseAddress"] = "http://localhost:5000/platform",
				["Distenka:Timeout"] = "00:00:30",
				["Distenka:AllowInsecureHttp"] = "true",
				["Distenka:UserAgent"] = "MyApp/1.2",
				["Distenka:MaxRetries"] = "5",
				["Distenka:MaxRetryDelay"] = "00:00:10",
			}).Build();

			using var provider = new ServiceCollection().AddDistenkaClient(configuration.GetSection("Distenka")).Services.BuildServiceProvider();

			var options = provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value;
			options.BaseAddress.Should().Be(new Uri("http://localhost:5000/platform"));
			options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
			options.AllowInsecureHttp.Should().BeTrue();
			options.UserAgent.Should().Be("MyApp/1.2");
			options.MaxRetries.Should().Be(5);
			options.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(10));

			provider.GetRequiredService<IDistenkaClient>().Should().NotBeNull();
		}

		[Theory]
		[InlineData("BaseAddress", "not a url")]
		[InlineData("Timeout", "soon")]
		[InlineData("AllowInsecureHttp", "maybe")]
		[InlineData("MaxRetries", "three")]
		[InlineData("MaxRetryDelay", "long")]
		public void unparsable_configuration_values_name_the_key(string key, string value)
		{
			var values = new Dictionary<string, string> { ["Distenka:BaseAddress"] = "https://api.example/", [$"Distenka:{key}"] = value };
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

			using var provider = new ServiceCollection().AddDistenkaClient(configuration.GetSection("Distenka")).Services.BuildServiceProvider();

			var read = () => provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value;

			read.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle(f => f.StartsWith($"Distenka:{key} "));
		}

		[Fact]
		public void a_configuration_without_base_address_fails_validation()
		{
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Distenka:Timeout"] = "00:00:30" }).Build();

			using var provider = new ServiceCollection().AddDistenkaClient(configuration.GetSection("Distenka")).Services.BuildServiceProvider();

			var resolve = () => provider.GetRequiredService<IDistenkaClient>();

			resolve.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain(f => f.Contains("BaseAddress is required"));
		}

		[Fact]
		public void the_public_constructor_validates_its_options()
		{
			using var http = new HttpClient();

			var construct = () => new DistenkaClient(http, new DistenkaClientOptions());

			construct.Should().Throw<OptionsValidationException>();
			((Action)(() => new DistenkaClient(null, new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/") }))).Should().Throw<ArgumentNullException>();
			((Action)(() => new DistenkaClient(http, null))).Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void registering_twice_registers_one_client_and_one_validator()
		{
			var services = new ServiceCollection();
			services.AddDistenkaClient(o => o.BaseAddress = new Uri("https://api.example"));
			services.AddDistenkaClient(o => o.Timeout = TimeSpan.FromSeconds(5));

			services.Count(d => d.ServiceType == typeof(IDistenkaClient)).Should().Be(1);
			services.Count(d => d.ServiceType == typeof(IValidateOptions<DistenkaClientOptions>)).Should().Be(1);

			using var provider = services.BuildServiceProvider();
			var options = provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value;
			options.BaseAddress.Should().Be(new Uri("https://api.example"));
			options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
		}
	}
}
