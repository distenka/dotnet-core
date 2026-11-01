using Distenka.Client.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-027 (AD-9): the account key reaches the client from configuration — never from a literal — and never appears in
	/// logs, option dumps or validation messages.
	/// </summary>
	public class Api_key_supply
	{
		/// <summary>A fake account key (48 hex digits); the hex part is what must never leak.</summary>
		const string Hex = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
		const string Key = "DK-" + Hex;

		[Fact]
		public async Task an_ApiKey_from_the_environment_becomes_an_account_key_credential()
		{
			// AC 1: AddDistenkaClient(configuration.GetSection("Distenka")) with Distenka__ApiKey set in the environment.
			var prefix = "DCS027_" + Guid.NewGuid().ToString("N") + "_";
			Environment.SetEnvironmentVariable(prefix + "Distenka__ApiKey", Key.ToLowerInvariant());
			try
			{
				var configuration = new ConfigurationBuilder()
					.AddInMemoryCollection(new Dictionary<string, string> { ["Distenka:BaseAddress"] = "https://api.example/" })
					.AddEnvironmentVariables(prefix)
					.Build();

				var handler = new ScriptedHttpHandler(Reply(404));
				using var provider = new ServiceCollection()
					.AddDistenkaClient(configuration.GetSection("Distenka"))
					.ConfigurePrimaryHttpMessageHandler(() => handler)
					.Services.BuildServiceProvider();

				var options = provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value;
				options.Credential.Should().BeOfType<AccountKeyCredential>();
				options.Credential.ToString().Should().Be("DK-01234567…", "only the public prefix is ever shown");

				await provider.GetRequiredService<IDistenkaClient>().Runs.TryGetDetailsAsync("r-1");

				var sent = handler.Requests.Single().Headers.Authorization;
				sent.Scheme.Should().Be("Bearer");
				sent.Parameter.Should().Be(Key, "the key is sent in canonical form");
			}
			finally
			{
				Environment.SetEnvironmentVariable(prefix + "Distenka__ApiKey", null);
			}
		}

		[Fact]
		public void without_an_ApiKey_the_configured_credential_is_left_alone()
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string> { ["Distenka:BaseAddress"] = "https://api.example/" })
				.Build();

			using var provider = new ServiceCollection()
				.AddDistenkaClient(configuration.GetSection("Distenka"))
				.Services.BuildServiceProvider();

			provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value.Credential.Should().BeNull();
		}

		[Theory]
		[InlineData("DK-NOTHEXNOTHEXNOTHEXNOTHEXNOTHEX99")]
		[InlineData("DKI-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
		[InlineData("sk_live_0123456789abcdef0123456789")]
		[InlineData("DK-01234")]
		public void an_ApiKey_that_is_not_an_account_key_fails_without_being_quoted(string value)
		{
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
			{
				["Distenka:BaseAddress"] = "https://api.example/",
				["Distenka:ApiKey"] = value,
			}).Build();

			using var provider = new ServiceCollection().AddDistenkaClient(configuration.GetSection("Distenka")).Services.BuildServiceProvider();

			var read = () => provider.GetRequiredService<IOptions<DistenkaClientOptions>>().Value;

			var thrown = read.Should().Throw<OptionsValidationException>().Which;
			thrown.Failures.Should().ContainSingle().Which.Should().StartWith("Distenka:ApiKey is not an account key.");
			thrown.Message.Should().NotContain(value.Substring(4), "the presented value is never echoed");
			thrown.Failures.Single().Should().NotContain(value.Substring(4));
		}

		[Fact]
		public async Task the_bearer_token_is_redacted_in_the_HttpClient_logs()
		{
			// The factory's logging handler writes request headers at Trace level.
			var logs = new CapturingLoggerProvider();
			var handler = new ScriptedHttpHandler(Reply(404));
			using var provider = new ServiceCollection()
				.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
				.AddDistenkaClient(o =>
				{
					o.BaseAddress = new Uri("https://api.example/");
					o.Credential = new AccountKeyCredential(Key);
				})
				.ConfigurePrimaryHttpMessageHandler(() => handler)
				.Services.BuildServiceProvider();

			await provider.GetRequiredService<IDistenkaClient>().Runs.TryGetDetailsAsync("r-1");

			handler.Requests.Should().ContainSingle().Which.Headers.Authorization.Parameter.Should().Be(Key);
			logs.Messages.Should().Contain(m => m.Contains("Authorization"), "headers are logged at Trace, so the test sees them");
			logs.Messages.Should().NotContain(m => m.Contains(Hex), string.Join(Environment.NewLine, logs.Messages));
		}

		[Fact]
		public void the_options_never_show_the_key()
		{
			var options = new DistenkaClientOptions { BaseAddress = new Uri("https://api.example/"), Credential = new AccountKeyCredential(Key) };

			options.ToString().Should().NotContain(Hex);
			options.Credential.ToString().Should().NotContain(Hex);
		}

		sealed class CapturingLoggerProvider : ILoggerProvider
		{
			readonly List<string> messages = new List<string>();

			public IReadOnlyList<string> Messages
			{
				get { lock (messages) return messages.ToList(); }
			}

			public ILogger CreateLogger(string categoryName) => new Logger(this);

			public void Dispose() { }

			sealed class Logger : ILogger
			{
				readonly CapturingLoggerProvider owner;

				public Logger(CapturingLoggerProvider owner) => this.owner = owner;

				public IDisposable BeginScope<TState>(TState state) => null;

				public bool IsEnabled(LogLevel logLevel) => true;

				public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
				{
					lock (owner.messages)
						owner.messages.Add(formatter(state, exception));
				}
			}
		}
	}
}
