using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Distenka
{
	/// <summary>
	/// Used to deserialize a simplified version of <see cref="ProcessorConfig"/> containing only the type name into the full class.
	/// </summary>
	/// <remarks>
	/// Deserializes a string into the full class. In the following JSON the value "Acme.Processor" would be placed in the <see cref="ProcessorConfig.Type"/> property.
	/// ```json
	/// {
	///   "Processor": "Acme.Processor"
	/// }
	/// ```
	/// </remarks>
	public class ProcessConfigConverter : JsonConverter
	{
		/// <summary>
		/// Determines whether this instance can convert the specified object type.
		/// </summary>
		public override bool CanConvert(Type objectType) => objectType == typeof(ProcessorConfig);

		/// <summary>
		/// Writes the JSON representation of the object.
		/// </summary>
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var cfg = value as ProcessorConfig;

			if (cfg == null)
			{
				writer.WriteNull();
			}
			else
			{
				if (String.IsNullOrWhiteSpace(cfg.Package) && String.IsNullOrWhiteSpace(cfg.Version))
				{
					writer.WriteValue(cfg.Type);
				}
				else
				{
					JToken t = JToken.FromObject(cfg, serializer);
					t.WriteTo(writer);
				}
			}
		}

		/// <summary>
		/// Reads the JSON representation of the object.
		/// </summary>
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return new ProcessorConfig();

			if (reader.TokenType == JsonToken.String)
				return new ProcessorConfig { Type = (string)reader.Value };

			var token = JToken.Load(reader);
			return token.ToObject<ProcessorConfig>();
		}
	}
}
