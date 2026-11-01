using System.Text.RegularExpressions;

namespace Distenka.Hosting
{
	/// <summary>
	/// Removes Distenka key values from text the SDK copies from user data onto stdout or into the run summary
	/// (DCS-046 AC 1, AD-9): <c>DK-</c>/<c>DKI-</c> followed by at least 32 hex digits keeps only its 8-digit public
	/// prefix, e.g. <c>DK-0123ABCD…</c> — the same display the client's credentials use. A processor may hold an
	/// owner-bound key in its configuration (DCS-027); if it reaches an item category, an exception message or the
	/// <c>FinalizeAsync</c> output, the SDK must not be the one to print it.
	/// </summary>
	/// <remarks>
	/// Applied to what the SDK writes itself: reporter lines that echo user text, the <c>ERROR:</c> line and the summary
	/// line. Text a processor logs or prints is not rewritten (ADR-004 §2.3 rule 4). Shape-based: a key that is split,
	/// encoded or in another format passes through.
	/// </remarks>
	internal static class SecretText
	{
		static readonly Regex Key = new Regex("(DKI?-[0-9A-F]{8})[0-9A-F]{24,}",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>Returns <paramref name="text"/> with every key-shaped value cut to its public prefix and <c>…</c>.</summary>
		public static string Redact(string text) =>
			string.IsNullOrEmpty(text) ? text : Key.Replace(text, "$1…");
	}
}
