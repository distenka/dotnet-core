using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-021 AC 5: the credential types add no acquire/cache/refresh member (checked by the same two tests).
	/// DCS-020 AC 5: approval test of the public API of <c>Distenka.Client</c>. A new public member must be added to
	/// <see cref="Approved"/> deliberately — and it may never be an account, login, token-refresh or key-management member
	/// (AD-9, ADR-006 §2.2). AC 3: no default host is compiled into the assembly.
	/// </summary>
	public class Api_surface
	{
		static readonly string[] Approved =
		{
			"Distenka.Client.Authentication.AccountKeyCredential",
			"Distenka.Client.Authentication.AccountKeyCredential::.ctor(String)",
			"Distenka.Client.Authentication.AccountKeyCredential::GetBearerTokenAsync(CancellationToken)",
			"Distenka.Client.Authentication.AccountKeyCredential::ToString()",
			"Distenka.Client.Authentication.InvocationKeyCredential",
			"Distenka.Client.Authentication.InvocationKeyCredential::.ctor(String)",
			"Distenka.Client.Authentication.InvocationKeyCredential::GetBearerTokenAsync(CancellationToken)",
			"Distenka.Client.Authentication.InvocationKeyCredential::ToString()",
			"Distenka.Client.Authentication.RunCapabilityCredential",
			"Distenka.Client.Authentication.RunCapabilityCredential::.ctor(String,Nullable`1)",
			"Distenka.Client.Authentication.RunCapabilityCredential::ExpiresAt",
			"Distenka.Client.Authentication.RunCapabilityCredential::GetBearerTokenAsync(CancellationToken)",
			"Distenka.Client.Authentication.RunCapabilityCredential::ToString()",
			"Distenka.Client.Authentication.IDistenkaCredential",
			"Distenka.Client.Authentication.IDistenkaCredential::GetBearerTokenAsync(CancellationToken)",
			"Distenka.Client.DistenkaApiException",
			"Distenka.Client.DistenkaApiException::.ctor(String,HttpStatusCode,String,String,Nullable`1,String,Nullable`1,String,Exception)",
			"Distenka.Client.DistenkaApiException::Body",
			"Distenka.Client.DistenkaApiException::Code",
			"Distenka.Client.DistenkaApiException::Error",
			"Distenka.Client.DistenkaApiException::IsRetryable",
			"Distenka.Client.DistenkaApiException::RawContent",
			"Distenka.Client.DistenkaApiException::RequestId",
			"Distenka.Client.DistenkaApiException::RetryAfter",
			"Distenka.Client.DistenkaApiException::StatusCode",
			"Distenka.Client.DistenkaClient",
			"Distenka.Client.DistenkaClient::.ctor(HttpClient,DistenkaClientOptions)",
			"Distenka.Client.DistenkaClient::Invocations",
			"Distenka.Client.DistenkaClient::Runs",
			"Distenka.Client.DistenkaClientOptions",
			"Distenka.Client.DistenkaClientOptions::.ctor()",
			"Distenka.Client.DistenkaClientOptions::AllowInsecureHttp",
			"Distenka.Client.DistenkaClientOptions::BaseAddress",
			"Distenka.Client.DistenkaClientOptions::Credential",
			"Distenka.Client.DistenkaClientOptions::Timeout",
			"Distenka.Client.DistenkaClientOptions::UserAgent",
			"Distenka.Client.DistenkaErrorCodes",
			"Distenka.Client.DistenkaErrorCodes::IdempotencyKeyMalformed",
			"Distenka.Client.DistenkaErrorCodes::IdempotencyKeyReuse",
			"Distenka.Client.DistenkaErrorCodes::IdempotencyKeyInFlight",
			"Distenka.Client.DistenkaErrorCodes::QueueDepthExceeded",
			"Distenka.Client.DistenkaErrorCodes::PlatformBackpressure",
			"Distenka.Client.DistenkaErrorCodes::PlatformOverloaded",
			"Distenka.Client.DistenkaErrorCodes::RateLimitExceeded",
			"Distenka.Client.DistenkaErrorCodes::QuotaExhausted",
			"Distenka.Client.DistenkaErrorCodes::RunAlreadyEnded",
			"Distenka.Client.DistenkaErrorCodes::ScopeViolation",
			"Distenka.Client.DistenkaErrorCodes::InvocationKeyRequired",
			"Distenka.Client.DistenkaErrorCodes::RunCapabilityRequired",
			"Distenka.Client.DistenkaErrorCodes::ProcessorUnavailable",
			"Distenka.Client.DistenkaErrorCodes::CallbacksUnavailable",
			"Distenka.Client.DistenkaErrorCodes::CapabilityUnavailable",
			"Distenka.Client.IDistenkaClient",
			"Distenka.Client.IDistenkaClient::Invocations",
			"Distenka.Client.IDistenkaClient::Runs",
			"Distenka.Client.Invocations.IInvocationsClient",
			"Distenka.Client.Runs.IRunsClient",
			"Microsoft.Extensions.DependencyInjection.DistenkaClientServiceCollectionExtensions",
			"Microsoft.Extensions.DependencyInjection.DistenkaClientServiceCollectionExtensions::AddDistenkaClient(IServiceCollection,Action`1)",
			"Microsoft.Extensions.DependencyInjection.DistenkaClientServiceCollectionExtensions::AddDistenkaClient(IServiceCollection,IConfiguration)",
		};

		/// <summary>Names that would mean an account / login / token-refresh / key-management surface.</summary>
		static readonly Regex Forbidden = new Regex(
			@"login|logon|logout|signin|signout|session|refresh|jwt|password|^(I?Account(s|Client)?)$|apikeys|(create|revoke|rotate|list|delete|issue)(api)?keys?",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		static Assembly ClientAssembly => typeof(DistenkaClient).Assembly;

		static IEnumerable<string> PublicSurface()
		{
			const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			static string Parameters(ParameterInfo[] ps) => string.Join(",", ps.Select(p => p.ParameterType.Name));

			foreach (var type in ClientAssembly.GetExportedTypes())
			{
				var name = type.FullName;
				yield return name;

				foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
					yield return $"{name}::.ctor({Parameters(c.GetParameters())})";
				foreach (var p in type.GetProperties(Declared))
					yield return $"{name}::{p.Name}";
				foreach (var m in type.GetMethods(Declared).Where(m => !m.IsSpecialName))
					yield return $"{name}::{m.Name}({Parameters(m.GetParameters())})";
				foreach (var f in type.GetFields(Declared))
					yield return $"{name}::{f.Name}";
				foreach (var e in type.GetEvents(Declared))
					yield return $"{name}::{e.Name}";
			}
		}

		[Fact]
		public void public_api_matches_the_approved_surface()
		{
			PublicSurface().OrderBy(x => x, StringComparer.Ordinal)
				.Should().Equal(Approved.OrderBy(x => x, StringComparer.Ordinal),
					"public API changes are deliberate: update Approved in the story that adds them");
		}

		[Fact]
		public void no_account_login_refresh_or_key_management_member()
		{
			var names = PublicSurface()
				.SelectMany(entry => entry.Split(new[] { "::", "." }, StringSplitOptions.RemoveEmptyEntries))
				.Select(part => part.Split('(')[0])
				.Distinct();

			names.Where(n => Forbidden.IsMatch(n)).Should().BeEmpty();
		}

		[Fact]
		public void the_forbidden_name_check_catches_what_it_should()
		{
			// Guard against a regex that silently matches nothing.
			new[] { "LoginAsync", "RefreshTokenAsync", "Account", "IAccountClient", "CreateKeyAsync", "RevokeApiKeyAsync", "Session" }
				.Should().OnlyContain(n => Forbidden.IsMatch(n));
			new[] { "AccountKeyCredential", "GetBearerTokenAsync", "Runs", "Invocations", "BaseAddress" }
				.Should().NotContain(n => Forbidden.IsMatch(n));
		}

		[Fact]
		public void no_default_host_is_compiled_into_the_assembly()
		{
			// AC 3 (AD-4): `distenka.ai` appears nowhere in the built assembly (UTF-8 metadata or UTF-16 user strings).
			var bytes = File.ReadAllBytes(ClientAssembly.Location);

			Contains(bytes, Encoding.UTF8.GetBytes("distenka.ai")).Should().BeFalse();
			Contains(bytes, Encoding.Unicode.GetBytes("distenka.ai")).Should().BeFalse();
		}

		static bool Contains(byte[] haystack, byte[] needle)
		{
			for (var i = 0; i <= haystack.Length - needle.Length; i++)
			{
				var j = 0;
				while (j < needle.Length && char.ToLowerInvariant((char)haystack[i + j]) == needle[j])
					j++;
				if (j == needle.Length)
					return true;
			}
			return false;
		}
	}
}
