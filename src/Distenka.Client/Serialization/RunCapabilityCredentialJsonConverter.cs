using Distenka.Client.Authentication;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Distenka.Client.Serialization
{
	/// <summary>
	/// Reads the invocation response's <c>resultCapability</c> string straight into a <see cref="RunCapabilityCredential"/>,
	/// so the raw capability never sits in a public string property (ADR-006 §2.1). A malformed value is a
	/// <see cref="JsonException"/> whose message does not contain the value.
	/// </summary>
	sealed class RunCapabilityCredentialJsonConverter : JsonConverter<RunCapabilityCredential>
	{
		public override RunCapabilityCredential? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;
			if (reader.TokenType != JsonTokenType.String)
				throw new JsonException($"A run capability must be a JSON string; got {reader.TokenType}.");

			try
			{
				return new RunCapabilityCredential(reader.GetString()!);
			}
			catch (ArgumentException)
			{
				throw new JsonException("The run capability is not a 'drc1.' value.");
			}
		}

		public override void Write(Utf8JsonWriter writer, RunCapabilityCredential value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.Token);
	}
}
