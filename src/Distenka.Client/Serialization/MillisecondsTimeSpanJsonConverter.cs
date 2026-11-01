using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Distenka.Client.Serialization
{
	/// <summary>A <see cref="TimeSpan"/>? carried as a JSON number of milliseconds (e.g. the timeline's <c>elapsed</c>).</summary>
	sealed class MillisecondsTimeSpanJsonConverter : JsonConverter<TimeSpan?>
	{
		public override bool HandleNull => true;

		public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;
			if (reader.TokenType != JsonTokenType.Number)
				throw new JsonException($"A duration in milliseconds must be a JSON number; got {reader.TokenType}.");

			return reader.TryGetInt64(out var whole) ? TimeSpan.FromTicks(whole * TimeSpan.TicksPerMillisecond) : TimeSpan.FromTicks((long)(reader.GetDouble() * TimeSpan.TicksPerMillisecond));
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
		{
			if (value is not TimeSpan span)
			{
				writer.WriteNullValue();
				return;
			}

			if (span.Ticks % TimeSpan.TicksPerMillisecond == 0)
				writer.WriteNumberValue(span.Ticks / TimeSpan.TicksPerMillisecond);
			else
				writer.WriteNumberValue((double)span.Ticks / TimeSpan.TicksPerMillisecond);
		}
	}
}
