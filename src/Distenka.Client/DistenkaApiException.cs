using System;
using System.Net;
using System.Text.Json;

namespace Distenka.Client
{
	/// <summary>
	/// A non-success response from the Distenka platform API. Carries the HTTP status, the platform's error
	/// <see cref="Code"/> and <see cref="Error"/> text when the body has them, the <c>Retry-After</c> delay and whether the
	/// failure is worth retrying.
	/// </summary>
	/// <remarks>
	/// The message never contains request headers, credentials or the request body (DCS-080). When the SDK creates the
	/// exception from a response, credential-shaped values the server or a proxy echoed back (<c>Bearer …</c>,
	/// <c>DK-</c>/<c>DKI-</c> keys beyond their 8-digit public prefix, <c>drc1.</c> capabilities) are redacted from
	/// <see cref="Exception.Message"/>, <see cref="Error"/>, <see cref="Body"/>, <see cref="RawContent"/> and
	/// <see cref="RequestId"/>, so logging the exception is safe. A 401 means the credential was rejected
	/// (missing, malformed, revoked or expired); the SDK never retries it or obtains a new token.
	/// </remarks>
	public class DistenkaApiException : Exception
	{
		/// <summary>The most characters of the response body kept in <see cref="RawContent"/>.</summary>
		internal const int RawContentLimit = 4096;

		/// <summary>Creates the exception. The SDK creates it from responses; the public constructor exists for tests and adapters.</summary>
		/// <param name="message">The message; must not contain secrets.</param>
		/// <param name="statusCode">The HTTP status code.</param>
		/// <param name="code">The platform error code, if any (see <see cref="DistenkaErrorCodes"/>).</param>
		/// <param name="error">The platform's human-readable error text, if any.</param>
		/// <param name="retryAfter">The delay from the <c>Retry-After</c> header, if any.</param>
		/// <param name="requestId">The request id from the response headers, if any.</param>
		/// <param name="body">The JSON error body, if the response had one.</param>
		/// <param name="rawContent">The response body as text, at most 4096 characters.</param>
		/// <param name="innerException">The underlying exception, if any.</param>
		public DistenkaApiException(string message, HttpStatusCode statusCode, string? code = null, string? error = null,
			TimeSpan? retryAfter = null, string? requestId = null, JsonElement? body = null, string? rawContent = null,
			Exception? innerException = null)
			: base(message, innerException)
		{
			StatusCode = statusCode;
			Code = code;
			Error = error;
			RetryAfter = retryAfter;
			RequestId = requestId;
			Body = body;
			RawContent = rawContent != null && rawContent.Length > RawContentLimit ? rawContent.Substring(0, RawContentLimit) : rawContent;
		}

		/// <summary>The HTTP status code of the response.</summary>
		public HttpStatusCode StatusCode { get; }

		/// <summary>
		/// The platform's error code (<c>code</c> in the body, matched case-insensitively), e.g.
		/// <see cref="DistenkaErrorCodes.IdempotencyKeyReuse"/>; <see langword="null"/> when the response has none (401, 404).
		/// </summary>
		public string? Code { get; }

		/// <summary>The platform's error text (<c>error</c> in the body), if any.</summary>
		public string? Error { get; }

		/// <summary>
		/// How long to wait before retrying, from the <c>Retry-After</c> header (seconds or HTTP date, measured from the
		/// response's <c>Date</c> header when present); <see langword="null"/> when absent.
		/// </summary>
		public TimeSpan? RetryAfter { get; }

		/// <summary>The request id, when the response carries an <c>X-Request-Id</c> or <c>Request-Id</c> header.</summary>
		public string? RequestId { get; }

		/// <summary>The JSON error body (other members such as <c>limit</c> are read from here); <see langword="null"/> when the body is not JSON.</summary>
		public JsonElement? Body { get; }

		/// <summary>
		/// The <c>limit</c> member of the error body, when present as a number — e.g. the request allowance on a
		/// <c>402 quota_exhausted</c>, or the queue limit on a <c>429 queue_depth_exceeded</c>.
		/// </summary>
		public long? Limit =>
			Body is JsonElement body && body.ValueKind == JsonValueKind.Object && body.TryGetProperty("limit", out var limit) &&
			limit.ValueKind == JsonValueKind.Number && limit.TryGetInt64(out var value)
				? value
				: (long?)null;

		/// <summary>The response body as text, truncated to 4096 characters; <see langword="null"/> when there was no body.</summary>
		public string? RawContent { get; }

		/// <summary>
		/// Whether the same request may succeed later: 408, 429, 502, 503 and 504, and 409
		/// <see cref="DistenkaErrorCodes.IdempotencyKeyInFlight"/>. Everything else is terminal — notably 400, 401, 402,
		/// 403, 404 and 409 <see cref="DistenkaErrorCodes.IdempotencyKeyReuse"/>. Whether the SDK actually retries also
		/// depends on the request being idempotent (AD-6).
		/// </summary>
		public bool IsRetryable => IsRetryableStatus(StatusCode, Code);

		internal static bool IsRetryableStatus(HttpStatusCode statusCode, string? code)
		{
			switch ((int)statusCode)
			{
				case 408:
				case 429:
				case 502:
				case 503:
				case 504:
					return true;
				case 409:
					return string.Equals(code, DistenkaErrorCodes.IdempotencyKeyInFlight, StringComparison.Ordinal);
				default:
					return false;
			}
		}
	}
}
