using Distenka.Client.Internal;
using Distenka.Client.Serialization;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Runs
{
	sealed class RunsClient : IRunsClient
	{
		internal RunsClient(HttpTransport transport) => Transport = transport;

		internal HttpTransport Transport { get; }

		public Task<RunAccepted> SubmitAsync<TConfig>(string processorId, TConfig configuration, JsonTypeInfo<TConfig> configurationTypeInfo,
			SubmitRunOptions? options = null, CancellationToken cancellationToken = default)
		{
			if (configurationTypeInfo == null)
				throw new ArgumentNullException(nameof(configurationTypeInfo));

			return SubmitAsync(processorId, JsonSerializer.SerializeToElement(configuration, configurationTypeInfo), options, cancellationToken);
		}

		public async Task<RunAccepted> SubmitAsync(string processorId, JsonElement configuration, SubmitRunOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			if (processorId == null || processorId.Trim().Length == 0)
				throw new ArgumentException("A processor id is required.", nameof(processorId));
			if (configuration.ValueKind != JsonValueKind.Object)
				throw new ArgumentException($"The run configuration must be a JSON object; it is {configuration.ValueKind}.", nameof(configuration));

			var idempotencyKey = options?.IdempotencyKey;
			if (idempotencyKey != null && idempotencyKey.Trim().Length == 0)
				throw new ArgumentException("An idempotency key must not be empty; leave it null to submit without one.", nameof(options));

			var body = SubmissionBody(processorId, configuration);

			HttpRequestMessage CreateRequest()
			{
				var request = Transport.CreateRequest(HttpMethod.Post, "api/runs");
				request.Content = new ByteArrayContent(body);
				request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
				if (idempotencyKey != null)
					request.Headers.Add(Resilience.RetryPolicy.IdempotencyKeyHeader, idempotencyKey);
				return request;
			}

			using var response = await Transport.SendWithRetryAsync(CreateRequest, HttpCompletionOption.ResponseContentRead, cancellationToken)
				.ConfigureAwait(false);

			var accepted = await ReadAsync(response, DistenkaJsonContext.Default.RunAccepted, cancellationToken).ConfigureAwait(false);
			if (accepted == null || accepted.RunId == null || accepted.RunId.Length == 0)
				throw new DistenkaApiException($"The Distenka API answered {(int)response.StatusCode} without a run id.", response.StatusCode);

			return new RunAccepted(accepted.RunId, accepted.ProcessorId, accepted.TrackingUrl, HttpTransport.IsReplayed(response));
		}

		/// <summary><c>{ "processorId": …, "configurationJson": { … } }</c> (<c>docs/api-run-submission.md</c> §The request).</summary>
		static byte[] SubmissionBody(string processorId, JsonElement configuration)
		{
			using var buffer = new MemoryStream();
			using (var writer = new Utf8JsonWriter(buffer))
			{
				writer.WriteStartObject();
				writer.WriteString("processorId", processorId);
				writer.WritePropertyName("configurationJson");
				configuration.WriteTo(writer);
				writer.WriteEndObject();
			}
			return buffer.ToArray();
		}

		static async Task<T?> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
			where T : class
		{
			try
			{
				using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
				return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
			}
			catch (JsonException e)
			{
				throw new DistenkaApiException($"The Distenka API answered {(int)response.StatusCode} with a body that is not the expected JSON.",
					response.StatusCode, innerException: e);
			}
		}
	}
}
