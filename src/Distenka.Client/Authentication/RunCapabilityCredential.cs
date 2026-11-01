using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Authentication
{
	/// <summary>
	/// A run-result capability (<c>drc1.…</c>) returned by an invocation; it grants read access to that run's result.
	/// </summary>
	/// <remarks>
	/// The value is opaque (ADR-006 §2.1): the SDK checks only that it starts with <c>drc1.</c> and contains no whitespace,
	/// and never decodes it. <see cref="ExpiresAt"/> comes from the invocation response, not from the token.
	/// <see cref="ToString"/> shows only <c>drc1.…</c>.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class RunCapabilityCredential : IDistenkaCredential
	{
		internal const string Prefix = "drc1.";

		readonly string token;

		/// <summary>Creates the credential from a run capability.</summary>
		/// <param name="capability">The <c>drc1.…</c> value; surrounding whitespace is ignored.</param>
		/// <param name="expiresAt">When the platform said the capability expires (<c>resultCapabilityExpiresAt</c>), if known. Informational only.</param>
		/// <exception cref="ArgumentNullException"><paramref name="capability"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException">The value does not start with <c>drc1.</c> or contains whitespace. The message never contains the value.</exception>
		public RunCapabilityCredential(string capability, DateTimeOffset? expiresAt = null)
		{
			if (capability == null)
				throw new ArgumentNullException(nameof(capability));

			var trimmed = capability.Trim();
			if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
				throw new ArgumentException($"Expected a run capability: a value starting with '{Prefix}'.", nameof(capability));

			foreach (var c in trimmed)
			{
				if (char.IsWhiteSpace(c))
					throw new ArgumentException("A run capability must not contain whitespace.", nameof(capability));
			}

			token = trimmed;
			ExpiresAt = expiresAt;
		}

		/// <summary>
		/// When the capability expires, as reported by the platform with the invocation; <see langword="null"/> when unknown.
		/// The SDK does not refuse to send an expired capability: the platform decides.
		/// </summary>
		public DateTimeOffset? ExpiresAt { get; }

		/// <summary>The capability, for writing it back in JSON (round-trips, tests). Never logged.</summary>
		internal string Token => token;

		/// <inheritdoc/>
		public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken) => new ValueTask<string>(token);

		/// <summary>Always <c>drc1.…</c>; the capability itself is never shown.</summary>
		public override string ToString() => Prefix + "…";
	}
}
