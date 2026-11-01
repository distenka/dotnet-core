using System;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// The platform's accept rule for DK-/DKI- keys (ADR-006 §2.1, mirroring <c>CredentialShape</c> / <c>ApiKeyFormat</c> @ 4c3cf2c):
	/// trimmed; marker matched case-insensitively; only hexadecimal after it; total length within the marker's range.
	/// The SDK sends the canonical form — exact-case marker, upper-case hex — because the platform routes by exact-case marker.
	/// Error messages state the expected shape and never contain the presented value beyond its marker.
	/// </summary>
	static class KeyShape
	{
		internal const int MaxLength = 128;

		internal static string Canonicalise(string value, string marker, int minLength, string ownDescription,
			string otherMarker, string otherDescription, string otherType, string paramName)
		{
			if (value == null)
				throw new ArgumentNullException(paramName);

			var trimmed = value.Trim();

			if (!trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
			{
				if (trimmed.StartsWith(otherMarker, StringComparison.OrdinalIgnoreCase))
					throw new ArgumentException($"The value is {otherDescription} ({otherMarker}…), not {ownDescription}; use {otherType} for it.", paramName);

				throw new ArgumentException(ShapeMessage(marker, minLength, ownDescription), paramName);
			}

			if (trimmed.Length < minLength || trimmed.Length > MaxLength)
				throw new ArgumentException(ShapeMessage(marker, minLength, ownDescription), paramName);

			for (var i = marker.Length; i < trimmed.Length; i++)
			{
				if (!IsHex(trimmed[i]))
					throw new ArgumentException(ShapeMessage(marker, minLength, ownDescription), paramName);
			}

			return marker + trimmed.Substring(marker.Length).ToUpperInvariant();
		}

		/// <summary>The public prefix the platform audits: marker + first 8 hex digits + "…".</summary>
		internal static string Redact(string canonical, string marker) =>
			canonical.Substring(0, Math.Min(canonical.Length, marker.Length + 8)) + "…";

		static string ShapeMessage(string marker, int minLength, string ownDescription) =>
			$"Expected {ownDescription}: '{marker}' followed by {minLength - marker.Length} to {MaxLength - marker.Length} hexadecimal characters.";

		static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
	}
}
