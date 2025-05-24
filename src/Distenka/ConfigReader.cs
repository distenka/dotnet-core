using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Distenka.Hosting;

namespace Distenka
{
	/// <summary>
	/// Deserializes JSON config files into instances of <see cref="Config"/>.
	/// </summary>
	public class ConfigReader
	{
		readonly ProcessCache cache;

		/// <summary>
		/// Initializes a new <see cref="ConfigReader"/>.
		/// </summary>
		/// <param name="cache">The <see cref="ProcessCache"/> containing the config types that can be read.</param>
		public ConfigReader(ProcessCache cache)
		{
			this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
		}

		/// <summary>
		/// Creates an instance of a <see cref="Config"/> from the JSON file found at <paramref name="path"/>.
		/// </summary>
		/// <param name="path">A file path contianing a JSON config.</param>
		/// <returns>An instance of <see cref="Config"/> from the deserialized file.</returns>
		public Config FromFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentNullException(nameof(path));

			if (!File.Exists(path))
				throw new FileNotFoundException("File not found", path);

			string configJson = null;

			using (var reader = new StreamReader(path))
			{
				configJson = reader.ReadToEnd();
			}

			return FromJson(configJson);
		}

		/// <summary>
		/// Creates an instance of a <see cref="Config"/> from the JSON string.
		/// </summary>
		/// <param name="json">A string containing a JSON config.</param>
		/// <returns>An instance of <see cref="Config"/> from the deserialized string.</returns>
		public Config FromJson(string json)
		{
			var processj = JObject.Parse(json);
			string type;

			if (IsReducedForm(processj))
			{
				// json is reduced form config, so get then remove 'Processor' before deserializing
				var Process = GetProcess(processj);
				type = Process.ToString();

                processj.Remove(Process.Path);
			}
			else
			{
				type = GetProcess(processj).ToObject<ProcessorConfig>().Type;
			}

			var ProcessInfo = cache.Get(type);

			if (ProcessInfo == null)
				throw new ConfigException($"Could not find Process type {type}.", nameof(Config.Process));

			if (!typeof(Config).IsAssignableFrom(ProcessInfo.ConfigType))
				throw new ArgumentException($"{ProcessInfo.ConfigType.FullName} does not extend {typeof(Config).FullName}.", nameof(ProcessInfo.ConfigType));

			var config = processj.ToObject(ProcessInfo.ConfigType) as Config;

			config.Process.Type = ProcessInfo.ProcessType.FullName;

			return config;
		}

		private bool IsReducedForm(JObject Process) => GetProcess(Process).Type == JTokenType.String;

		private JToken GetProcess(JObject Process) => Process.GetValue(nameof(Process), StringComparison.OrdinalIgnoreCase);
	}
}
