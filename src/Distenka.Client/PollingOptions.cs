using System;

namespace Distenka.Client
{
	/// <summary>
	/// How the client polls until a run has finished (ADR-007 §polling discipline): geometric back-off from
	/// <see cref="InitialInterval"/> to <see cref="MaxInterval"/>, <c>Retry-After</c> honoured, and never more than
	/// <see cref="MaxPollsPerMinute"/> requests in any minute.
	/// </summary>
	public sealed class PollingOptions
	{
		/// <summary>The wait after the first poll. Default 2 seconds.</summary>
		public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(2);

		/// <summary>The longest wait between two polls. Default 30 seconds.</summary>
		public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>Each wait is the previous one times this factor (at least 1), up to <see cref="MaxInterval"/>. Default 1.5.</summary>
		public double BackoffMultiplier { get; set; } = 1.5;

		/// <summary>
		/// How long to wait in total before giving up with a <see cref="TimeoutException"/>. <see langword="null"/> (default):
		/// until the credential's expiry, when known, otherwise until cancelled.
		/// </summary>
		public TimeSpan? Timeout { get; set; }

		/// <summary>
		/// The most requests sent in any 60 seconds, retries included. Default 12 (one every five seconds). The platform counts
		/// every poll, a <c>304</c> included, against the key's own result-poll window.
		/// </summary>
		public int MaxPollsPerMinute { get; set; } = 12;
	}
}
