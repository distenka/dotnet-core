using Distenka.Client.Authentication;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>DCS-021: the three credentials and their shape rules (ADR-006 §2.1).</summary>
	public class Credentials
	{
		const string Hex48 = "0123456789abcdef0123456789abcdef0123456789abcdef";
		const string Hex32 = "0123456789abcdef0123456789abcdef";

		static async Task<string> Token(IDistenkaCredential credential) => await credential.GetBearerTokenAsync(CancellationToken.None);

		// ---- AccountKeyCredential ----------------------------------------------------------------------------------------

		[Fact]
		public async Task account_key_is_sent_in_canonical_form()
		{
			// AC 3: `dk-` + lower-case hex is accepted and sent as `DK-` + upper-case hex.
			(await Token(new AccountKeyCredential("dk-" + Hex48))).Should().Be("DK-" + Hex48.ToUpperInvariant());
			(await Token(new AccountKeyCredential("  DK-" + Hex48.ToUpperInvariant() + "\n"))).Should().Be("DK-" + Hex48.ToUpperInvariant());
		}

		[Fact]
		public async Task legacy_32_hex_account_key_is_accepted() =>
			(await Token(new AccountKeyCredential("DK-" + Hex32))).Should().Be("DK-" + Hex32.ToUpperInvariant());

		[Theory]
		[InlineData(8)]   // total 11, the platform's minimum
		[InlineData(125)] // total 128, the maximum
		public void account_key_length_bounds_are_inclusive(int hexDigits) =>
			new AccountKeyCredential("DK-" + new string('a', hexDigits)).Should().NotBeNull();

		[Theory]
		[InlineData("DK-")]
		[InlineData("DK-0123456")]                  // 10 chars
		[InlineData("DK-0123456789ABCDEFXYZ0123")]  // non-hex
		[InlineData("DK-0123 4567 89AB CDEF")]      // inner whitespace
		[InlineData("0123456789ABCDEF0123")]        // no marker
		[InlineData("Bearer DK-0123456789ABCDEF")]  // header value, not a key
		[InlineData("drc1.0123456789ABCDEF")]
		[InlineData("")]
		[InlineData("   ")]
		public void malformed_account_keys_are_rejected_without_the_value_in_the_message(string value)
		{
			var create = () => new AccountKeyCredential(value);

			var thrown = create.Should().Throw<ArgumentException>().Which;
			thrown.ParamName.Should().Be("accountKey");
			if (value.Trim().Length > 3)
				thrown.Message.Should().NotContain(value.Trim().Substring(3));
			thrown.Message.Should().Contain("'DK-' followed by 8 to 125 hexadecimal characters");
		}

		[Fact]
		public void account_key_longer_than_128_is_rejected() =>
			((Action)(() => new AccountKeyCredential("DK-" + new string('a', 126)))).Should().Throw<ArgumentException>();

		[Fact]
		public void an_invocation_key_is_not_an_account_key()
		{
			var create = () => new AccountKeyCredential("dki-" + Hex48);

			var thrown = create.Should().Throw<ArgumentException>().Which;
			thrown.Message.Should().Contain("invocation key").And.Contain(nameof(InvocationKeyCredential)).And.NotContain(Hex48[..8]);
		}

		[Fact]
		public void null_is_rejected()
		{
			((Action)(() => new AccountKeyCredential(null))).Should().Throw<ArgumentNullException>().WithParameterName("accountKey");
			((Action)(() => new InvocationKeyCredential(null))).Should().Throw<ArgumentNullException>().WithParameterName("invocationKey");
			((Action)(() => new RunCapabilityCredential(null))).Should().Throw<ArgumentNullException>().WithParameterName("capability");
		}

		// ---- InvocationKeyCredential -------------------------------------------------------------------------------------

		[Fact]
		public async Task invocation_key_is_sent_in_canonical_form() =>
			(await Token(new InvocationKeyCredential(" dki-" + Hex48 + " "))).Should().Be("DKI-" + Hex48.ToUpperInvariant());

		[Theory]
		[InlineData(8)]   // total 12
		[InlineData(124)] // total 128
		public void invocation_key_length_bounds_are_inclusive(int hexDigits) =>
			new InvocationKeyCredential("DKI-" + new string('b', hexDigits)).Should().NotBeNull();

		[Theory]
		[InlineData("DKI-0123456")]   // 11 chars
		[InlineData("DKI-XYZ0123456789")]
		[InlineData("DKI")]
		public void malformed_invocation_keys_are_rejected(string value)
		{
			var create = () => new InvocationKeyCredential(value);

			create.Should().Throw<ArgumentException>().Which.Message.Should().Contain("'DKI-' followed by 8 to 124 hexadecimal characters");
		}

		[Fact]
		public void invocation_key_longer_than_128_is_rejected() =>
			((Action)(() => new InvocationKeyCredential("DKI-" + new string('b', 125)))).Should().Throw<ArgumentException>();

		[Fact]
		public void an_account_key_is_not_an_invocation_key()
		{
			var create = () => new InvocationKeyCredential("DK-" + Hex48);

			var thrown = create.Should().Throw<ArgumentException>().Which;
			thrown.Message.Should().Contain("account key").And.Contain(nameof(AccountKeyCredential)).And.NotContain(Hex48[..8]);
		}

		// ---- RunCapabilityCredential -------------------------------------------------------------------------------------

		[Fact]
		public async Task run_capability_is_opaque()
		{
			// AC 3: no code path decodes the payload — anything after `drc1.` without whitespace is passed through as-is.
			(await Token(new RunCapabilityCredential("drc1.!!not-base64~~"))).Should().Be("drc1.!!not-base64~~");
			(await Token(new RunCapabilityCredential("  drc1.abc  "))).Should().Be("drc1.abc");
		}

		[Fact]
		public void run_capability_expiry_is_informational()
		{
			var expiry = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

			new RunCapabilityCredential("drc1.abc", expiry).ExpiresAt.Should().Be(expiry);
			new RunCapabilityCredential("drc1.abc").ExpiresAt.Should().BeNull();
			new RunCapabilityCredential("drc1.abc", DateTimeOffset.MinValue).Should().NotBeNull("an expired capability is the platform's to refuse");
		}

		[Theory]
		[InlineData("DRC1.abc")]       // the marker is exact-case
		[InlineData("drc2.abc")]
		[InlineData("drc1.ab c")]
		[InlineData("drc1.ab\tc")]
		[InlineData("DK-0123456789ABCDEF")]
		public void malformed_run_capabilities_are_rejected(string value)
		{
			var create = () => new RunCapabilityCredential(value);

			create.Should().Throw<ArgumentException>().Which.Message.Should().NotContain("abc").And.NotContain("0123456789");
		}

		// ---- Redaction (AC 4) --------------------------------------------------------------------------------------------

		[Fact]
		public void ToString_shows_the_public_prefix_only()
		{
			new AccountKeyCredential("dk-" + Hex48).ToString().Should().Be("DK-01234567…");
			new InvocationKeyCredential("DKI-" + Hex48).ToString().Should().Be("DKI-01234567…");
			new RunCapabilityCredential("drc1.secretpayload").ToString().Should().Be("drc1.…");
		}

		[Fact]
		public void debugger_display_uses_the_redacted_form()
		{
			foreach (var type in new[] { typeof(AccountKeyCredential), typeof(InvocationKeyCredential), typeof(RunCapabilityCredential) })
				type.GetCustomAttributes(typeof(DebuggerDisplayAttribute), false).Cast<DebuggerDisplayAttribute>()
					.Should().ContainSingle().Which.Value.Should().Be("{ToString(),nq}");
		}

		// ---- On the wire (AC 1, AC 5, re-homed DCS-020 AC 1) ---------------------------------------------------------------

		[Fact]
		public async Task AddDistenkaClient_with_an_AccountKeyCredential_sends_the_key_as_a_bearer_token()
		{
			// Re-homed from DCS-020 AC 1.
			var handler = new RecordingHandler();
			using var provider = new ServiceCollection()
				.AddDistenkaClient(o =>
				{
					o.BaseAddress = new Uri("https://api.example");
					o.Credential = new AccountKeyCredential("dk-" + Hex48);
				})
				.ConfigurePrimaryHttpMessageHandler(() => handler)
				.Services.BuildServiceProvider();

			var client = (DistenkaClient)provider.GetRequiredService<IDistenkaClient>();
			using var request = client.Transport.CreateRequest(HttpMethod.Get, "api/runs");
			using var _ = await client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

			var sent = handler.Requests.Single();
			sent.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "DK-" + Hex48.ToUpperInvariant()));
			sent.Headers.Select(h => h.Key).Should().NotContain(k =>
				string.Equals(k, "X-Product-Key", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "x-api-key", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public async Task a_401_is_returned_as_is_and_never_retried_with_a_new_token()
		{
			var handler = new StatusHandler(HttpStatusCode.Unauthorized);
			var credential = new CountingCredential(new AccountKeyCredential("DK-" + Hex48));
			var client = new DistenkaClient(new HttpClient(handler), new DistenkaClientOptions { BaseAddress = new Uri("https://api.example"), Credential = credential });

			using var request = client.Transport.CreateRequest(HttpMethod.Get, "api/runs");
			using var response = await client.Transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

			response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
			handler.Calls.Should().Be(1);
			credential.Calls.Should().Be(1);
		}

		sealed class StatusHandler : HttpMessageHandler
		{
			readonly HttpStatusCode status;
			public StatusHandler(HttpStatusCode status) => this.status = status;
			public int Calls { get; private set; }

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Calls++;
				return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
			}
		}

		sealed class CountingCredential : IDistenkaCredential
		{
			readonly IDistenkaCredential inner;
			public CountingCredential(IDistenkaCredential inner) => this.inner = inner;
			public int Calls { get; private set; }

			public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken)
			{
				Calls++;
				return inner.GetBearerTokenAsync(cancellationToken);
			}
		}
	}
}
