using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Resilience
{
	/// <summary>
	/// When and how long to wait before repeating a request (DCS-025, AD-6). Used by <c>HttpTransport.SendWithRetryAsync</c>
	/// rather than a <see cref="DelegatingHandler"/>, so a caller-supplied <see cref="HttpClient"/> gets the same behaviour
	/// as the one <c>AddDistenkaClient</c> creates.
	/// </summary>
	/// <remarks>
	/// A request is repeated only when it is safe to repeat (<see cref="IsReplaySafe"/>, or an operation the platform
	/// documents as idempotent) and the failure is retryable (<see cref="DistenkaApiException.IsRetryable"/> or a network
	/// error). The wait is the platform's <c>Retry-After</c> when given — never more than <see cref="MaxDelay"/>; a longer
	/// <c>Retry-After</c> ends the retries — and otherwise decorrelated jitter starting at 200 ms.
	/// </remarks>
	sealed class RetryPolicy
	{
		internal const string IdempotencyKeyHeader = "Idempotency-Key";
		internal static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);

		static readonly Random SharedRandom = new Random();
		static readonly object RandomLock = new object();

		readonly Func<TimeSpan, CancellationToken, Task> delay;
		readonly Func<double> random;

		internal RetryPolicy(int maxRetries, TimeSpan maxDelay, Func<TimeSpan, CancellationToken, Task>? delay = null, Func<double>? random = null)
		{
			MaxRetries = maxRetries;
			MaxDelay = maxDelay;
			Func<TimeSpan, CancellationToken, Task> defaultDelay = (wait, token) => Task.Delay(wait, token);
			Func<double> defaultRandom = NextRandom;
			this.delay = delay ?? defaultDelay;
			this.random = random ?? defaultRandom;
		}

		internal int MaxRetries { get; }

		internal TimeSpan MaxDelay { get; }

		/// <summary><c>GET</c> and <c>HEAD</c> are safe to repeat; so is any request carrying an <c>Idempotency-Key</c>.</summary>
		internal static bool IsReplaySafe(HttpRequestMessage request) =>
			request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.Headers.Contains(IdempotencyKeyHeader);

		/// <summary>
		/// The wait before the next attempt, or <see langword="null"/> when the platform asked for more than
		/// <see cref="MaxDelay"/> (the caller should schedule the retry itself from <c>RetryAfter</c>).
		/// </summary>
		/// <param name="retryAfter">The platform's <c>Retry-After</c>, if any.</param>
		/// <param name="previous">The previous jittered wait; updated. Start with <see cref="BaseDelay"/>.</param>
		internal TimeSpan? NextDelay(TimeSpan? retryAfter, ref TimeSpan previous)
		{
			if (retryAfter is TimeSpan requested)
				return requested > MaxDelay ? (TimeSpan?)null : requested;

			// Decorrelated jitter: uniform in [base, previous * 3], capped.
			var upper = Math.Max(BaseDelay.Ticks, previous.Ticks * 3);
			var ticks = BaseDelay.Ticks + (long)(random() * (upper - BaseDelay.Ticks));
			var next = TimeSpan.FromTicks(Math.Min(ticks, MaxDelay.Ticks));
			previous = next;
			return next;
		}

		internal Task DelayAsync(TimeSpan wait, CancellationToken cancellationToken) => delay(wait, cancellationToken);

		static double NextRandom()
		{
			lock (RandomLock)
				return SharedRandom.NextDouble();
		}
	}
}
