using Distenka.Client.Authentication;
using Distenka.Client.Internal;
using Distenka.Client.Resilience;
using Distenka.Client.Serialization;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Invocations
{
	sealed class InvocationsClient : IInvocationsClient
	{
		internal const string CallbackUrlHeader = "Callback-Url";

		internal InvocationsClient(HttpTransport transport)
		{
			Transport = transport;
			Results = new RunResultClient(transport);
		}

		internal HttpTransport Transport { get; }

		internal RunResultClient Results { get; }

		public Task<RunResultResponse> GetResultAsync(RunCapabilityCredential capability, string? etag = null,
			CancellationToken cancellationToken = default) =>
			Results.GetResultAsync(capability, etag, cancellationToken);

		public Task<RunResult> WaitForResultAsync(RunCapabilityCredential capability, PollingOptions? options = null,
			CancellationToken cancellationToken = default) =>
			Results.WaitForResultAsync(capability, options, cancellationToken);

		public Task<InvocationAccepted> InvokeAsync<TInput>(string pluginId, TInput input, JsonTypeInfo<TInput> inputTypeInfo,
			InvokeOptions? options = null, CancellationToken cancellationToken = default)
		{
			if (inputTypeInfo == null)
				throw new ArgumentNullException(nameof(inputTypeInfo));

			return InvokeAsync(pluginId, JsonSerializer.SerializeToElement(input, inputTypeInfo), options, cancellationToken);
		}

		public async Task<InvocationAccepted> InvokeAsync(string pluginId, JsonElement input, InvokeOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			if (pluginId == null || pluginId.Trim().Length == 0)
				throw new ArgumentException("A plugin id is required.", nameof(pluginId));
			if (input.ValueKind != JsonValueKind.Object)
				throw new ArgumentException($"The invocation input must be a JSON object; it is {input.ValueKind}.", nameof(input));

			var idempotencyKey = options?.IdempotencyKey;
			if (idempotencyKey != null && idempotencyKey.Trim().Length == 0)
				throw new ArgumentException("An idempotency key must not be empty; leave it null to invoke without one.", nameof(options));

			var callbackUrl = options?.CallbackUrl;
			if (callbackUrl != null && !callbackUrl.IsAbsoluteUri)
				throw new ArgumentException("The callback URL must be absolute.", nameof(options));

			var body = Bytes(input);
			var path = "v1/invoke/" + Uri.EscapeDataString(pluginId.Trim());

			HttpRequestMessage CreateRequest()
			{
				var request = Transport.CreateRequest(HttpMethod.Post, path);
				request.Content = new ByteArrayContent(body);
				request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
				if (idempotencyKey != null)
					request.Headers.Add(RetryPolicy.IdempotencyKeyHeader, idempotencyKey);
				if (callbackUrl != null)
					request.Headers.TryAddWithoutValidation(CallbackUrlHeader, callbackUrl.AbsoluteUri);
				return request;
			}

			using var response = await Transport.SendWithRetryAsync(CreateRequest, HttpCompletionOption.ResponseContentRead, cancellationToken)
				.ConfigureAwait(false);

			var accepted = await ResponseReader.ReadAsync(response, DistenkaJsonContext.Default.InvocationAccepted, cancellationToken).ConfigureAwait(false);
			if (accepted == null || accepted.RunId == null || accepted.RunId.Length == 0)
				throw new DistenkaApiException($"The Distenka API answered {(int)response.StatusCode} without a run id.", response.StatusCode);

			// The capability's expiry travels next to it in the body; attach it so callers can stop polling before a 401.
			var capability = accepted.ResultCapability == null
				? null
				: new RunCapabilityCredential(accepted.ResultCapability.Token, accepted.ResultCapabilityExpiresAt);

			return new InvocationAccepted(accepted.RunId, accepted.Status, capability, accepted.ResultCapabilityExpiresAt,
				HttpTransport.IsReplayed(response));
		}

		static byte[] Bytes(JsonElement input)
		{
			using var buffer = new MemoryStream();
			using (var writer = new Utf8JsonWriter(buffer))
				input.WriteTo(writer);
			return buffer.ToArray();
		}
	}
}
