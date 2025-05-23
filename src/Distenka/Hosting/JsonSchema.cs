using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema.Generation;
using System;

namespace Distenka.Hosting
{
	/// <summary>
	/// Generates a JSON schema for a <see cref="Config"/>.
	/// </summary>
	public class JsonSchema
	{
		readonly JsonSchemaGenerator schema;
		readonly ILogger<JsonSchema> logger;

		/// <summary>
		/// Initializes a new <see cref="JsonSchema"/>.
		/// </summary>
		/// <param name="logger">A <see cref="ILogger"/>.</param>
		public JsonSchema(ILogger<JsonSchema> logger)
		{
			this.logger = logger;

			schema = new JsonSchemaGenerator(new JsonSchemaGeneratorSettings
			{
				FlattenInheritanceHierarchy = true,
				SerializerSettings = ConfigWriter.Settings
			});
		}

        /// <summary>
        /// Generates a JSON schema for the <paramref name="configType"/>.
        /// </summary>
        /// <param name="configType">The <see cref="Config"/> to generate the JSON schema for.</param>
        /// <returns>A <see cref="JObject"/> containing the schema.</returns>
        public JObject Generate(Type configType)
		{
			// We have to serialize and then deserialize the JsonSchema again in order
			// to go through NJsonSchema's ToJson method. It does some post-processing
			// of the JSON making it where we can't just serialize the JsonSchema directly.

			string json = schema.Generate(configType).ToJson();
			logger.LogDebug("Serialized JSON schema for {configType}: {jsonSchema}", configType, json);

            JObject obj = JsonConvert.DeserializeObject<JObject>(json);

			var ProcessTypes = LoadProcessTypeArray(obj);
			ProcessTypes.Add(JToken.FromObject(new
			{
				type = "string",
				description = "The short or full name of the type of Processor. The package and version will be inferred."
			}));

			return obj;
		}

		JArray LoadProcessTypeArray(JObject obj)
		{
			var Process = (JObject)obj["properties"]["Process"];

			var oneOf = Process["oneOf"];
			if (oneOf != null)
				return oneOf.Value<JArray>();

			var arr = new JArray();
			var newProcess = new JObject();
			newProcess.Add("oneOf", arr);

			Process.Replace(newProcess);
			arr.Add(Process);

			return arr;
		}
	}
}
