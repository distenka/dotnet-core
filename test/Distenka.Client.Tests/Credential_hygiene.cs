using Distenka.Client.Authentication;
using Distenka.Client.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using static Distenka.Client.Tests.ScriptedHttpHandler;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-080: no credential — account key (<c>DK-</c>), invocation key (<c>DKI-</c>) or run capability (<c>drc1.</c>) —
	/// ever appears in a <see cref="DistenkaApiException"/>, a <c>ToString()</c>, the HttpClient logs or console output,
	/// including when the server or a proxy echoes it back.
	/// </summary>
	public class Credential_hygiene
	{
		const string AccountHex = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
		const string AccountKey = "DK-" + AccountHex;
		const string InvokeHex = "FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210";
		const string InvokeKey = "DKI-" + InvokeHex;
		const string CapabilitySecret = "eyJydW4iOiJyLTEifQ.c2lnbmF0dXJlLXNlY3JldA";
		const string Capability = "drc1." + CapabilitySecret;

		/// <summary>The secret parts; the public prefixes (<c>DK-01234567</c>, <c>drc1.</c>) may be shown.</summary>
		static readonly string[] Secrets = { AccountHex, InvokeHex, CapabilitySecret };

		static readonly JsonElement Input = JsonDocument.Parse(@"{""message"":""hello""}").RootElement.Clone();

		static string Token(string kind) => kind switch
		{
			"account" => AccountKey,
			"invocation" => InvokeKey,
			_ => Capability,
		};

		/// <summary>Runs the operation each credential is used for and returns the exception it throws.</summary>
		static async Task<DistenkaApiException> FailAsync(string kind, ScriptedHttpHandler handler)
		{
			IDistenkaClient client = new DistenkaClient(new HttpClient(handler), new DistenkaClientOptions
			{
				BaseAddress = new Uri("https://api.example/"),
				Credential = kind == "invocation" ? new InvocationKeyCredential(InvokeKey) : new AccountKeyCredential(AccountKey),
				MaxRetries = 0,
			});

			Func<Task> call = kind switch
			{
				"account" => () => client.Runs.GetDetailsAsync("r-1"),
				"invocation" => () => client.Invocations.InvokeAsync("echo", Input),
				_ => () => client.Invocations.GetResultAsync(new RunCapabilityCredential(Capability)),
			};

			var thrown = (await call.Should().ThrowAsync<DistenkaApiException>()).Which;
			handler.Requests.Single().Headers.Authorization.Parameter.Should().Be(Token(kind), "the credential was really sent");
			return thrown;
		}

		/// <summary>Everything the exception carries, as text.</summary>
		static string[] Texts(DistenkaApiException thrown) =>
			new[] { thrown.Message, thrown.ToString(), thrown.Error, thrown.RawContent, thrown.RequestId, thrown.Body?.GetRawText() }
				.Where(t => t != null).ToArray();

		static void NoSecretIn(params string[] texts)
		{
			foreach (var text in texts)
				foreach (var secret in Secrets)
					text.Should().NotContainEquivalentOf(secret, "no credential may appear in: " + text);
		}

		[Theory]
		[InlineData("account", 401, null)]
		[InlineData("account", 404, null)]
		[InlineData("account", 500, "<html><body>Internal error</body></html>")]
		[InlineData("invocation", 400, @"{""code"":""idempotency_key_malformed"",""error"":""The Idempotency-Key header is malformed.""}")]
		[InlineData("invocation", 403, @"{""code"":""scope_violation"",""error"":""The key may not invoke this processor.""}")]
		[InlineData("invocation", 429, @"{""code"":""rate_limit_exceeded"",""error"":""Too many requests.""}")]
		[InlineData("capability", 401, null)]
		[InlineData("capability", 503, @"{""code"":""capability_unavailable"",""error"":""Try again.""}")]
		public async Task a_failed_call_never_puts_the_credential_in_the_exception(string kind, int status, string body)
		{
			// AC 1.
			var thrown = await FailAsync(kind, new ScriptedHttpHandler(Reply(status, body)));

			((int)thrown.StatusCode).Should().Be(status);
			NoSecretIn(Texts(thrown));
		}

		[Theory]
		[InlineData("account", "DK-01234567…")]
		[InlineData("invocation", "DKI-FEDCBA98…")]
		[InlineData("capability", "drc1.…")]
		public async Task a_credential_echoed_by_the_server_is_redacted(string kind, string shown)
		{
			// A server or proxy that quotes the presented credential must not turn a logged exception into a leak.
			var token = Token(kind);
			var body = $@"{{""code"":""invalid_credential"",""error"":""Rejected Authorization: Bearer {token}"",""presented"":""{token}""}}";

			var thrown = await FailAsync(kind, new ScriptedHttpHandler(Reply(401, body, ("X-Request-Id", token))));

			NoSecretIn(Texts(thrown));
			thrown.Code.Should().Be("invalid_credential", "redaction leaves the rest of the body intact");
			thrown.Error.Should().Be("Rejected Authorization: Bearer *");
			thrown.Message.Should().Contain("Bearer *");
			thrown.Body.Value.GetProperty("presented").GetString().Should().Be(shown, "only the public prefix survives");
			thrown.RequestId.Should().Be(shown);
		}

		[Fact]
		public async Task a_proxy_page_listing_the_request_headers_is_redacted()
		{
			var page = "<html><pre>GET /api/runs/r-1/details\nauthorization: bearer " + AccountKey.ToLowerInvariant() +
				"\nx-capability: " + Capability + "\n</pre></html>";

			var thrown = await FailAsync("account", new ScriptedHttpHandler(Reply(502, page)));

			NoSecretIn(Texts(thrown));
			thrown.RawContent.Should().Contain("authorization: bearer *").And.Contain("x-capability: drc1.…");
		}

		[Theory]
		[InlineData("DK-0123ABCD", "DK-0123ABCD")]
		[InlineData("invokedByKeyPrefix: DKI-0123ABCD", "invokedByKeyPrefix: DKI-0123ABCD")]
		[InlineData("key DK-0123456789abcdef0123456789abcdef", "key DK-01234567…")]
		[InlineData("dki-0123456789abcdef01234567", "dki-01234567…")]
		[InlineData("\"drc1.abc.def-_xyz\"", "\"drc1.…\"")]
		[InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.e30.sig", "Authorization: Bearer *")]
		[InlineData("Bearer DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", "Bearer *")]
		[InlineData("a bearer token was expected", "a bearer token was expected")]
		[InlineData("", "")]
		public void the_redactor_keeps_public_prefixes_and_removes_secrets(string text, string expected)
		{
			SecretRedactor.Redact(text).Should().Be(expected);
		}

		[Fact]
		public async Task per_call_credentials_are_redacted_in_the_HttpClient_logs()
		{
			// AC 2: the factory logs under System.Net.Http.HttpClient.Distenka.Client.* with headers at Trace. DCS-027 covered
			// the configured account key; this covers the invocation key and the per-call run capability.
			var accepted = $@"{{""runId"":""r-1"",""status"":""Pending"",""resultCapability"":""{Capability}"",""resultCapabilityExpiresAt"":""2026-09-19T10:02:11+00:00""}}";
			var logs = new CapturingLoggerProvider();
			var handler = new ScriptedHttpHandler(Reply(202, accepted), Reply(401));
			using var provider = new ServiceCollection()
				.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs))
				.AddDistenkaClient(o =>
				{
					o.BaseAddress = new Uri("https://api.example/");
					o.Credential = new InvocationKeyCredential(InvokeKey);
					o.MaxRetries = 0;
				})
				.ConfigurePrimaryHttpMessageHandler(() => handler)
				.Services.BuildServiceProvider();
			var client = provider.GetRequiredService<IDistenkaClient>();

			var invocation = await client.Invocations.InvokeAsync("echo", Input);
			var thrown = (await ((Func<Task>)(() => client.Invocations.GetResultAsync(invocation.ResultCapability)))
				.Should().ThrowAsync<DistenkaApiException>()).Which;

			handler.Requests.Select(r => r.Headers.Authorization.Parameter).Should().Equal(new[] { InvokeKey, Capability }, "both credentials were sent");
			logs.Entries.Should().Contain(e => e.Category.StartsWith("System.Net.Http.HttpClient.Distenka.Client", StringComparison.Ordinal) &&
				e.Message.Contains("Authorization"), "headers are logged at Trace under the client's category, so the test sees them");
			NoSecretIn(logs.Messages.ToArray());
			NoSecretIn(Texts(thrown));
			NoSecretIn(invocation.ToString(), invocation.ResultCapability.ToString());
		}

		[Fact]
		public void the_client_never_writes_to_the_console_or_debug_output()
		{
			// The deleted 1.x client did Console.WriteLine(config). Nothing in Distenka.Client may write to the console or the
			// Debug/Trace listeners: checked on the compiled assembly (net8.0) and on the sources (covers netstandard2.0-only code).
			var forbidden = new[] { "System.Console", "System.Diagnostics.Debug", "System.Diagnostics.Trace" };

			using (var stream = File.OpenRead(typeof(DistenkaClient).Assembly.Location))
			using (var pe = new PEReader(stream))
			{
				var metadata = pe.GetMetadataReader();
				var referenced = metadata.TypeReferences
					.Select(h => metadata.GetTypeReference(h))
					.Select(t => metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name))
					.ToList();
				referenced.Should().Contain("System.Net.Http.HttpClient", "the scan reads the real type references");
				referenced.Should().NotIntersectWith(forbidden);
			}

			var root = new DirectoryInfo(AppContext.BaseDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "Distenka.sln")))
				root = root.Parent;
			root.Should().NotBeNull("the tests run from inside the repository");

			var source = Path.Combine(root.FullName, "src", "Distenka.Client");
			var call = new Regex(@"\b(Console|Debug|Trace)\s*\.\s*[A-Z]", RegexOptions.CultureInvariant);
			var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
				.Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
					!f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
				.ToList();
			files.Should().NotBeEmpty();

			var offending = files
				.SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Number: i + 1, Line: line.Trim())))
				.Where(l => !l.Line.StartsWith("//", StringComparison.Ordinal) && call.IsMatch(l.Line))
				.Select(l => $"{Path.GetRelativePath(root.FullName, l.File)}:{l.Number}: {l.Line}")
				.ToList();
			offending.Should().BeEmpty();
		}
	}
}
