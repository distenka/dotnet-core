using Distenka.Client.Authentication;
using Distenka.Client.Internal;
using Distenka.Client.Runs;
using Distenka.Client.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Invocations
{
	/// <summary>
	/// Reads an invoked run's result with its run-result capability — <c>GET /api/runs/result</c> (DCS-062,
	/// <c>docs/api-run-results.md</c>, ADR-007). The capability is sent as the request's bearer token instead of the
	/// configured credential, so the invocation key never travels to this route.
	/// </summary>
	sealed class RunResultClient
	{
		internal const string ResultPath = "api/runs/result";
		static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

		readonly HttpTransport transport;
		readonly Func<DateTimeOffset> clock;
		readonly Func<TimeSpan, CancellationToken, Task> delay;

		internal RunResultClient(HttpTransport transport)
			: this(transport, null, null)
		{
		}

		/// <param name="transport">Sends the requests.</param>
		/// <param name="clock">The current time (tests inject a fake one); default <see cref="DateTimeOffset.UtcNow"/>.</param>
		/// <param name="delay">Waits between polls (tests record them); default the transport's retry delay.</param>
		internal RunResultClient(HttpTransport transport, Func<DateTimeOffset>? clock, Func<TimeSpan, CancellationToken, Task>? delay)
		{
			this.transport = transport;
			Func<DateTimeOffset> defaultClock = () => DateTimeOffset.UtcNow;
			Func<TimeSpan, CancellationToken, Task> defaultDelay = transport.RetryPolicy.DelayAsync;
			this.clock = clock ?? defaultClock;
			this.delay = delay ?? defaultDelay;
		}

		internal async Task<RunResultResponse> GetResultAsync(RunCapabilityCredential capability, string? etag, CancellationToken cancellationToken)
		{
			if (capability == null)
				throw new ArgumentNullException(nameof(capability));

			var token = await capability.GetBearerTokenAsync(cancellationToken).ConfigureAwait(false);
			var validator = Normalize(etag);

			using var response = await transport.SendWithRetryAsync(() => CreateRequest(token, validator), HttpCompletionOption.ResponseContentRead,
				cancellationToken, acceptNotModified: true).ConfigureAwait(false);

			if (response.StatusCode == HttpStatusCode.NotModified)
				return new RunResultResponse(null, ReadETag(response) ?? validator, notModified: true);

			return new RunResultResponse(await ReadResultAsync(response, cancellationToken).ConfigureAwait(false), ReadETag(response), notModified: false);
		}

		internal async Task<RunResult> WaitForResultAsync(RunCapabilityCredential capability, PollingOptions? options, CancellationToken cancellationToken)
		{
			if (capability == null)
				throw new ArgumentNullException(nameof(capability));

			options ??= new PollingOptions();
			Validate(options);

			var token = await capability.GetBearerTokenAsync(cancellationToken).ConfigureAwait(false);
			var start = clock();

			// Never poll past the capability's expiry: from then on every answer is a 401 (ADR-007).
			DateTimeOffset? deadline = null;
			var deadlineIsExpiry = false;
			if (options.Timeout is TimeSpan timeout)
				deadline = start + timeout;
			if (capability.ExpiresAt is DateTimeOffset expiresAt && (deadline == null || expiresAt < deadline.Value))
			{
				deadline = expiresAt;
				deadlineIsExpiry = true;
			}

			var sent = new Queue<DateTimeOffset>();
			var interval = options.InitialInterval;
			string? etag = null;
			RunResult? last = null;

			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var now = clock();
				while (sent.Count > 0 && now - sent.Peek() >= OneMinute)
					sent.Dequeue();

				if (deadline is DateTimeOffset end && now >= end)
					throw TimedOut(last, deadlineIsExpiry);

				if (sent.Count >= options.MaxPollsPerMinute)
				{
					// The budget is spent: wait until the oldest request in the window is a minute old.
					await WaitAsync(sent.Peek() + OneMinute - now, deadline, cancellationToken).ConfigureAwait(false);
					continue;
				}

				sent.Enqueue(now);
				TimeSpan? retryAfter = null;
				try
				{
					using var request = CreateRequest(token, etag);
					using var response = await transport.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

					if (response.StatusCode == HttpStatusCode.NotModified)
					{
						etag = ReadETag(response) ?? etag;
					}
					else if (response.IsSuccessStatusCode)
					{
						last = await ReadResultAsync(response, cancellationToken).ConfigureAwait(false);
						etag = ReadETag(response);
						if (last.State.IsTerminal())
							return last;
					}
					else
					{
						var error = await ApiErrorReader.CreateAsync(response, clock(), cancellationToken).ConfigureAwait(false);

						// 404: the capability is good but the run is not there yet (archival lag) — documented as retryable here.
						if (response.StatusCode != HttpStatusCode.NotFound && !error.IsRetryable)
							throw error;

						retryAfter = error.RetryAfter;
					}
				}
				catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
				{
					// A network failure: the read is safe to repeat at the next interval.
				}

				var wait = interval;
				if (retryAfter is TimeSpan requested && requested > wait)
					wait = requested;

				interval = TimeSpan.FromTicks((long)Math.Min(options.MaxInterval.Ticks, interval.Ticks * options.BackoffMultiplier));

				await WaitAsync(wait, deadline, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Waits <paramref name="wait"/>, but not past <paramref name="deadline"/> (the loop then ends with a timeout).</summary>
		async Task WaitAsync(TimeSpan wait, DateTimeOffset? deadline, CancellationToken cancellationToken)
		{
			if (deadline is DateTimeOffset end)
			{
				var left = end - clock();
				if (left < wait)
					wait = left;
			}

			if (wait > TimeSpan.Zero)
				await delay(wait, cancellationToken).ConfigureAwait(false);
		}

		HttpRequestMessage CreateRequest(string token, string? etag)
		{
			var request = transport.CreateRequest(HttpMethod.Get, ResultPath);

			// Set before the transport prepares the request, so the configured credential (the invocation key) is not added.
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			if (etag != null)
				request.Headers.TryAddWithoutValidation("If-None-Match", etag);
			return request;
		}

		static async Task<RunResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
		{
			var result = await ResponseReader.ReadAsync(response, DistenkaJsonContext.Default.RunResult, cancellationToken).ConfigureAwait(false);
			if (result == null || result.RunId == null || result.RunId.Length == 0)
				throw new DistenkaApiException($"The Distenka API answered {(int)response.StatusCode} without a run id.", response.StatusCode);
			return result;
		}

		static string? ReadETag(HttpResponseMessage response) =>
			response.Headers.TryGetValues("ETag", out var values) ? Normalize(values.FirstOrDefault()) : null;

		static string? Normalize(string? etag) => etag is string value && value.Trim().Length > 0 ? value.Trim() : null;

		static TimeoutException TimedOut(RunResult? last, bool capabilityExpired)
		{
			var why = capabilityExpired ? "before its result capability expired" : "within the polling timeout";
			var status = last?.Status ?? "not read yet";
			return new TimeoutException($"The run did not finish {why} (last status: {status}).");
		}

		static void Validate(PollingOptions options)
		{
			if (options.InitialInterval <= TimeSpan.Zero)
				throw new ArgumentException("PollingOptions.InitialInterval must be positive.", nameof(options));
			if (options.MaxInterval < options.InitialInterval)
				throw new ArgumentException("PollingOptions.MaxInterval must be at least InitialInterval.", nameof(options));
			if (double.IsNaN(options.BackoffMultiplier) || options.BackoffMultiplier < 1 || options.BackoffMultiplier > 10)
				throw new ArgumentException("PollingOptions.BackoffMultiplier must be between 1 and 10.", nameof(options));
			if (options.Timeout is TimeSpan timeout && timeout <= TimeSpan.Zero)
				throw new ArgumentException("PollingOptions.Timeout must be positive when set.", nameof(options));
			if (options.MaxPollsPerMinute < 1)
				throw new ArgumentException("PollingOptions.MaxPollsPerMinute must be at least 1.", nameof(options));
		}
	}
}
