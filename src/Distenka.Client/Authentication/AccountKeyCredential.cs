using Distenka.Client.Internal;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Authentication
{
	/// <summary>
	/// An account key (<c>DK-…</c>): the SDK's execution credential for the <see cref="IDistenkaClient.Runs"/> surface (AD-9).
	/// Keys are issued in the console; supply the value from configuration or a secret store, never as a literal in code.
	/// </summary>
	/// <remarks>
	/// The value is checked against the platform's accept rule (ADR-006 §2.1): marker <c>DK-</c> in any case, then only
	/// hexadecimal characters, 11–128 characters in total, so legacy 32-hex keys are accepted. It is sent in canonical
	/// form (<c>DK-</c> + upper-case hex). The SDK never refreshes or replaces a key: an expired or revoked key surfaces
	/// as a 401 from the platform. <see cref="ToString"/> shows only the public prefix.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class AccountKeyCredential : IDistenkaCredential
	{
		internal const string Marker = "DK-";

		readonly string token;

		/// <summary>Creates the credential from an account key.</summary>
		/// <param name="accountKey">The <c>DK-…</c> key; surrounding whitespace is ignored.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="accountKey"/> is <see langword="null"/>.</exception>
		/// <exception cref="System.ArgumentException">
		/// The value is not an account key — for example an invocation key (<c>DKI-…</c>). The message never contains the value.
		/// </exception>
		public AccountKeyCredential(string accountKey) =>
			token = KeyShape.Canonicalise(accountKey, Marker, 11, "an account key",
				InvocationKeyCredential.Marker, "an invocation key", nameof(InvocationKeyCredential), nameof(accountKey));

		/// <inheritdoc/>
		public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken) => new ValueTask<string>(token);

		/// <summary>The public prefix only, e.g. <c>DK-1A2B3C4D…</c>.</summary>
		public override string ToString() => KeyShape.Redact(token, Marker);
	}
}
