using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Internal
{
	/// <summary>Reads a success response's JSON body with the client's source-generated contracts.</summary>
	static class ResponseReader
	{
		/// <summary>The body as <typeparamref name="T"/>; a body that is not the expected JSON is a <see cref="DistenkaApiException"/>.</summary>
		internal static async Task<T?> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
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
