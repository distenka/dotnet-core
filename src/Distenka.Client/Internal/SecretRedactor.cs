using System.Text.RegularExpressions;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// Removes credential-shaped values from text the SDK did not write itself — a response body, error text or header
	/// value — before it reaches an exception (DCS-080). A server or proxy that echoes the presented credential
	/// (an error page listing the request headers, an error message quoting the token) must not turn the exception's
	/// <c>Message</c>/<c>ToString()</c> into a credential leak in the caller's logs.
	/// </summary>
	/// <remarks>
	/// Shapes (ADR-006): <c>Bearer &lt;token of 16+ characters&gt;</c> → <c>Bearer *</c> (the same mark the HttpClient log redaction writes);
	/// <c>drc1.&lt;…&gt;</c> → <c>drc1.…</c>; <c>DK-</c>/<c>DKI-</c> followed by more than 8 hex digits → the marker and
	/// the 8-digit public prefix plus <c>…</c>, as the credentials' <c>ToString()</c> shows them. A bare public prefix
	/// (exactly 8 hex digits, e.g. a run's <c>invokedByKeyPrefix</c>) is not a secret and is left as it is. Markers match
	/// case-insensitively, wider than the platform's exact-case rule, so a lower-cased echo is caught too.
	/// </remarks>
	static class SecretRedactor
	{
		/// <summary>What a token is made of: anything up to whitespace, a quote, a markup bracket, a separator or a JSON escape.</summary>
		const string TokenChars = @"[^\s""'<>,;\\]+";

		const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

		/// <summary>At least 16 token characters, so prose such as "a bearer token was expected" is left alone; every real credential is longer.</summary>
		static readonly Regex Bearer = new Regex(@"(Bearer)\s+[^\s""'<>,;\\]{16,}", Options);

		static readonly Regex Capability = new Regex(@"drc1\." + TokenChars, Options);

		static readonly Regex Key = new Regex(@"(DKI?-[0-9A-F]{8})[0-9A-F]+", Options);

		/// <summary>Returns <paramref name="text"/> with every credential-shaped value replaced; <see langword="null"/> stays <see langword="null"/>.</summary>
		internal static string? Redact(string? text)
		{
			if (text == null || text.Length == 0)
				return text;

			var redacted = Bearer.Replace(text, "$1 *");
			redacted = Capability.Replace(redacted, "drc1.…");
			return Key.Replace(redacted, "$1…");
		}
	}
}
