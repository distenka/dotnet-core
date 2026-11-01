using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;

namespace Distenka
{
	/// <summary>
	/// Methods for serializing <see cref="Config">Configs</see>.
	/// </summary>
	public static class ConfigWriter
	{
		/// <summary>
		/// Settings used for the <see cref="JsonSerializer"/>.
		/// </summary>
		public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			ContractResolver = new DefaultContractResolver
			{
				NamingStrategy = new CamelCaseNamingStrategy
				{
					ProcessDictionaryKeys = false
				}
			},
			Formatting = Formatting.Indented
		};

		/// <summary>
		/// A <see cref="JsonSerializer"/> created using the <see cref="Settings"/>.
		/// </summary>
		public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

		/// <summary>
		/// Serializes a <see cref="Config"/> to JSON.
		/// </summary>
		public static string ToJson(Config config) => JsonConvert.SerializeObject(config, Settings) + Environment.NewLine;

		/// <summary>
		/// Serializes a <see cref="Config"/> to JSON, removing the <see cref="Config.Execution"/> section for brevity.
		/// </summary>
		public static string ToReducedJson(Config config) => JsonConvert.SerializeObject(ToReducedProcessject(config), Settings) + Environment.NewLine;

		/// <summary>
		/// Serializes a <see cref="Config"/> as a runnable example: the reduced form (no <see cref="Config.Execution"/>, the
		/// process as its canonical name) without root members whose default value is <c>null</c> — the published input
		/// schema declares them non-nullable, so a <c>null</c> would be refused by the platform (ADR-003; DCS-043).
		/// </summary>
		public static string ToExampleJson(Config config)
		{
			var json = ToReducedProcessject(config);

			foreach (var property in json.Properties().Where(p => p.Value.Type == JTokenType.Null).ToList())
				property.Remove();

			return JsonConvert.SerializeObject(json, Settings) + Environment.NewLine;
		}

		internal static JObject ToReducedProcessject(Config config)
		{
			config.Process.Version = null;
			config.Process.Package = null;

			var Process = JObject.FromObject(config, Serializer);

			Process.Remove(nameof(Config.Execution).ToCamelCase());

			return Process;
		}
	}
}
