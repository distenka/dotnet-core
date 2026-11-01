using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Distenka.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Distenka
{
	/// <summary>
	/// Deserializes JSON config files into instances of <see cref="Config"/>.
	/// </summary>
	/// <remarks>
	/// Resolution and validation follow ADR-002 §2.2/§2.4 and ADR-003 §2.4 (DCS-044): the process is named by the
	/// document's <c>process</c> string (or the legacy <c>{ "type": … }</c> object), may be omitted when the package has
	/// exactly one process, and is resolved by canonical name, short name, then case-insensitively. Members the resolved
	/// configuration type requires must be present, and DataAnnotations attributes are enforced. Every problem is reported
	/// as a <see cref="ConfigException"/> whose message is a single line suitable for an <c>ERROR:</c> line.
	/// </remarks>
	public class ConfigReader
	{
		/// <summary>Maximum number of problems listed in one <see cref="ConfigException"/> message.</summary>
		const int MaxProblems = 10;

		const string ProcessMember = "process";

		static readonly IContractResolver Resolver = ConfigWriter.Settings.ContractResolver;

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
		/// <exception cref="ConfigException">The document is not a valid configuration for this package.</exception>
		public Config FromFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentNullException(nameof(path));

			if (!File.Exists(path))
				throw new FileNotFoundException("File not found", path);

			return FromJson(File.ReadAllText(path));
		}

		/// <summary>
		/// Creates an instance of a <see cref="Config"/> from a JSON document.
		/// </summary>
		/// <exception cref="ConfigException">The document is not a valid configuration for this package.</exception>
		public Config FromJson(string json)
		{
			JObject root;

			try
			{
				root = JToken.Parse(json ?? string.Empty) as JObject;
			}
			catch (JsonReaderException ex)
			{
				throw new ConfigException($"configuration is not valid JSON: {ex.Message}", null, ex);
			}

			if (root is null)
				throw new ConfigException("configuration must be a JSON object", null);

			var info = ResolveProcess(root);

			if (!typeof(Config).IsAssignableFrom(info.ConfigType))
				throw new ArgumentException($"{info.ConfigType.FullName} does not extend {typeof(Config).FullName}.", nameof(info.ConfigType));

			var problems = new List<string>();
			CheckRequiredMembersPresent(root, info, problems);

			Config config = null;

			if (problems.Count == 0)
			{
				try
				{
					config = (Config)root.ToObject(info.ConfigType);
				}
				catch (JsonException ex)
				{
					problems.Add(ex.Message);
				}
			}

			if (config != null)
			{
				config.Process.Type = info.ProcessType.FullName;
				Validate(config, "", problems, new HashSet<object>(ReferenceEqualityComparer.Instance), depth: 0);
			}

			if (problems.Count > 0)
			{
				var listed = string.Join("; ", problems.Take(MaxProblems)) +
					(problems.Count > MaxProblems ? $"; and {problems.Count - MaxProblems} more" : "");

				throw new ConfigException($"configuration for {info.ProcessType.FullName} is invalid: {listed}", null);
			}

			return config;
		}

		/// <summary>
		/// ADR-002 §2.4: finds and removes the <c>process</c> member and resolves the process it names, or the only
		/// process of the package when it is absent or <c>null</c>.
		/// </summary>
		ProcessInfo ResolveProcess(JObject root)
		{
			var member = root.Properties().FirstOrDefault(p => string.Equals(p.Name, ProcessMember, StringComparison.OrdinalIgnoreCase));
			var token = member?.Value;
			member?.Remove();

			var valid = cache.ValidProcesses;
			string name;

			switch (token?.Type)
			{
				case null:
				case JTokenType.Null:
					if (valid.Count == 1)
						return valid[0];

					if (valid.Count == 0)
						throw new ConfigException("the package contains no runnable process", ProcessMember);

					throw new ConfigException(
						$"configuration does not name a process and the package contains {valid.Count} processes ({string.Join(", ", valid.Select(p => p.ProcessType.FullName))})",
						ProcessMember);

				case JTokenType.String:
					name = (string)token;
					break;

				case JTokenType.Object:
					// Legacy 1.x form { "type", "package", "version" }: resolves on type only (ADR-002 §2.2 rule 5).
					var type = ((JObject)token).Properties().FirstOrDefault(p => string.Equals(p.Name, "type", StringComparison.OrdinalIgnoreCase))?.Value;
					if (type?.Type != JTokenType.String)
						throw new ConfigException("process.type: must be a string naming the process", ProcessMember);
					name = (string)type;
					break;

				default:
					throw new ConfigException("process: must be a string naming the process", ProcessMember);
			}

			try
			{
				return cache.Get(name);
			}
			catch (TypeNotFoundException ex)
			{
				throw new ConfigException(ex.Message, ProcessMember, ex);
			}
		}

		/// <summary>
		/// ADR-002 §2.3 rule 4 / §2.4: a member the resolved configuration type requires (<c>[Required]</c>, or
		/// <c>[JsonProperty(Required = …)]</c> other than <c>Default</c>) must be present in the document. Value-type
		/// members cannot be checked by DataAnnotations once deserialized (a missing <c>int</c> is just <c>0</c>).
		/// </summary>
		static void CheckRequiredMembersPresent(JObject root, ProcessInfo info, List<string> problems)
		{
			if (Resolver.ResolveContract(info.ConfigType) is not JsonObjectContract contract)
				return;

			foreach (var property in contract.Properties)
			{
				if (property.Ignored || !property.Writable || property.DeclaringType == typeof(Config))
					continue;

				var required = property.Required != Required.Default ||
					(property.AttributeProvider?.GetAttributes(typeof(RequiredAttribute), true).Count ?? 0) > 0;

				if (!required)
					continue;

				var present = root.Properties().Any(p =>
					string.Equals(p.Name, property.PropertyName, StringComparison.OrdinalIgnoreCase) ||
					string.Equals(p.Name, property.UnderlyingName, StringComparison.OrdinalIgnoreCase));

				if (!present)
					problems.Add($"{property.PropertyName}: required by {info.ProcessType.FullName} but missing");
			}
		}

		/// <summary>
		/// ADR-003 §2.4: enforces DataAnnotations attributes (including those with no descriptor keyword, such as
		/// <c>[RegularExpression]</c> or <c>[EmailAddress]</c>) on the configuration and its nested objects.
		/// </summary>
		static void Validate(object instance, string path, List<string> problems, HashSet<object> visited, int depth)
		{
			if (instance is null || depth > DescriptorSchemaGenerator.MaxDepth || !visited.Add(instance))
				return;

			var type = instance.GetType();
			var contract = Resolver.ResolveContract(type) as JsonObjectContract;

			var results = new List<ValidationResult>();
			Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

			foreach (var result in results)
			{
				var members = result.MemberNames.Select(m => SerializedName(contract, m)).ToList();
				var where = members.Count == 0 ? (string.IsNullOrEmpty(path) ? "configuration" : path) : string.Join(", ", members.Select(m => Join(path, m)));
				problems.Add($"{where}: {result.ErrorMessage}");
			}

			if (contract is null)
				return;

			foreach (var property in contract.Properties)
			{
				if (property.Ignored || !property.Readable || property.DeclaringType == typeof(Config) || property.ValueProvider is null)
					continue;

				object value;
				try
				{
					value = property.ValueProvider.GetValue(instance);
				}
				catch
				{
					continue;
				}

				if (value is null || IsLeaf(value.GetType()))
					continue;

				var memberPath = Join(path, property.PropertyName);

				if (value is IEnumerable sequence)
				{
					var index = 0;
					foreach (var item in sequence)
					{
						if (item != null && !IsLeaf(item.GetType()))
							Validate(item, $"{memberPath}[{index}]", problems, visited, depth + 1);
						index++;
					}
				}
				else
				{
					Validate(value, memberPath, problems, visited, depth + 1);
				}
			}
		}

		static bool IsLeaf(Type type) =>
			type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
			type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri) ||
			type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(byte[]) ||
			(type.IsArray && IsLeaf(type.GetElementType()));

		static string SerializedName(JsonObjectContract contract, string member) =>
			contract?.Properties.FirstOrDefault(p => p.UnderlyingName == member)?.PropertyName ?? member;

		static string Join(string path, string name) => string.IsNullOrEmpty(path) ? name : $"{path}.{name}";
	}
}
