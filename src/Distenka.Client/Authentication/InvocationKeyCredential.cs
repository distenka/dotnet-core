using Distenka.Client.Internal;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Authentication
{
	/// <summary>
	/// An invocation key (<c>DKI-…</c>) for the <see cref="IDistenkaClient.Invocations"/> surface. Supply the value from
	/// configuration or a secret store, never as a literal in code.
	/// </summary>
	/// <remarks>
	/// The value is checked against the platform's accept rule (ADR-006 §2.1): marker <c>DKI-</c> in any case, then only
	/// hexadecimal characters, 12–128 characters in total. It is sent in canonical form (<c>DKI-</c> + upper-case hex).
	/// The SDK never refreshes or replaces a key. <see cref="ToString"/> shows only the public prefix.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class InvocationKeyCredential : IDistenkaCredential
	{
		internal const string Marker = "DKI-";

		readonly string token;

		/// <summary>Creates the credential from an invocation key.</summary>
		/// <param name="invocationKey">The <c>DKI-…</c> key; surrounding whitespace is ignored.</param>
		/// <exception cref="System.ArgumentNullException"><paramref name="invocationKey"/> is <see langword="null"/>.</exception>
		/// <exception cref="System.ArgumentException">
		/// The value is not an invocation key — for example an account key (<c>DK-…</c>). The message never contains the value.
		/// </exception>
		public InvocationKeyCredential(string invocationKey) =>
			token = KeyShape.Canonicalise(invocationKey, Marker, 12, "an invocation key",
				AccountKeyCredential.Marker, "an account key", nameof(AccountKeyCredential), nameof(invocationKey));

		/// <inheritdoc/>
		public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken) => new ValueTask<string>(token);

		/// <summary>The public prefix only, e.g. <c>DKI-1A2B3C4D…</c>.</summary>
		public override string ToString() => KeyShape.Redact(token, Marker);
	}
}
