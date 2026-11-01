using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// Turns a non-success response into a <see cref="DistenkaApiException"/> (DCS-022). Replaces the deleted 1.x
	/// <c>HttpExtensions.EnsureSuccess</c>, which only threw when reading the body failed. Reads at most
	/// <see cref="BodyReadLimit"/> bytes of the body; never looks at request headers. Credential-shaped values in the body
	/// and the request-id header are redacted (<see cref="SecretRedactor"/>, DCS-080).
	/// </summary>
	static class ApiErrorReader
	{
		/// <summary>Error bodies are small; this bounds memory for a misbehaving proxy page.</summary>
		internal const int BodyReadLimit = 64 * 1024;

		static readonly string[] RequestIdHeaders = { "X-Request-Id", "Request-Id" };

		/// <summary>Returns when <paramref name="response"/> is a success; otherwise throws a <see cref="DistenkaApiException"/>.</summary>
		internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
		{
			if (response == null)
				throw new ArgumentNullException(nameof(response));
			if (response.IsSuccessStatusCode)
				return;

			throw await CreateAsync(response, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
		}

		/// <param name="now">The clock used for an HTTP-date <c>Retry-After</c> when the response has no <c>Date</c> header.</param>
		internal static async Task<DistenkaApiException> CreateAsync(HttpResponseMessage response, DateTimeOffset now, CancellationToken cancellationToken)
		{
			var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);

			// DCS-080: a server or proxy may echo the presented credential. Redact once, then derive everything the exception
			// carries — Code, Error, Body, RawContent and the message — from the redacted text.
			var raw = bytes.Length == 0 ? null : SecretRedactor.Redact(Encoding.UTF8.GetString(bytes));

			JsonElement? body = null;
			string? code = null, error = null;
			if (raw != null)
			{
				try
				{
					using var document = JsonDocument.Parse(raw);
					if (document.RootElement.ValueKind == JsonValueKind.Object)
					{
						body = document.RootElement.Clone();
						code = StringMember(document.RootElement, "code");
						error = StringMember(document.RootElement, "error");
					}
				}
				catch (JsonException)
				{
					// Not JSON (an HTML error page from a proxy, plain text): keep the raw content only.
				}
			}

			var status = response.StatusCode;
			var retryAfter = RetryAfter(response, now);
			var requestId = RequestId(response);

			var message = new StringBuilder("The Distenka API returned ")
				.Append((int)status).Append(' ').Append(status);
			if (code != null)
				message.Append(" (").Append(code).Append(')');
			message.Append(error != null ? ": " + Bound(error, 512) : ".");

			return new DistenkaApiException(message.ToString(), status, code, error, retryAfter, requestId, body, raw);
		}

		static async Task<byte[]> ReadBoundedAsync(HttpContent? content, CancellationToken cancellationToken)
		{
			if (content == null)
				return Array.Empty<byte>();

			using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
			using var buffer = new MemoryStream();
			var chunk = new byte[8192];
			int read;
			while (buffer.Length < BodyReadLimit &&
				(read = await stream.ReadAsync(chunk, 0, (int)Math.Min(chunk.Length, BodyReadLimit - buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
			{
				buffer.Write(chunk, 0, read);
			}

			return buffer.ToArray();
		}

		/// <summary>Reads a string member by name, case-insensitively (<c>code</c> and <c>Code</c> both occur).</summary>
		static string? StringMember(JsonElement element, string name)
		{
			foreach (var property in element.EnumerateObject())
			{
				if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
					return property.Value.GetString();
			}
			return null;
		}

		static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now)
		{
			var header = response.Headers.RetryAfter;
			if (header == null)
				return null;

			if (header.Delta is TimeSpan delta)
				return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;

			if (header.Date is DateTimeOffset date)
			{
				var reference = response.Headers.Date ?? now;
				var wait = date - reference;
				return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
			}

			return null;
		}

		static string? RequestId(HttpResponseMessage response)
		{
			foreach (var name in RequestIdHeaders)
			{
				if (response.Headers.TryGetValues(name, out var values) && values.FirstOrDefault() is string value && value.Trim().Length > 0)
					return Bound(SecretRedactor.Redact(value.Trim())!, 256);
			}
			return null;
		}

		static string Bound(string text, int max) => text.Length <= max ? text : text.Substring(0, max) + "…";
	}
}
