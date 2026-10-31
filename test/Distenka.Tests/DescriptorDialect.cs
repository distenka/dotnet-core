using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Distenka.Tests
{
	/// <summary>
	/// A port of the platform's descriptor-dialect rules (<c>PluginInputSchema.TryParse</c>) and of the executor's
	/// <c>ExtractJsonObject</c>, as recorded in ADR-003 §1.1 from <c>D:\Product\Distenka</c> @ <c>4c3cf2c</c>
	/// (<c>Distenka.Core.API/Services/PluginInputSchema.cs</c>; <c>Distenka.Core.Executor/Program.cs:676-704</c>).
	/// Minimal port for DCS-043; the reusable harness is DCS-093.
	/// </summary>
	static class DescriptorDialect
	{
		public static readonly string[] Keywords =
		{
			"type", "required", "nullable", "description", "default", "enum", "minimum", "maximum",
			"minLength", "maxLength", "minItems", "maxItems", "items", "properties"
		};

		static readonly string[] Types = { "string", "number", "integer", "boolean", "object", "array" };

		/// <summary>Returns the reasons the platform would call the schema unusable (empty = usable).</summary>
		public static List<string> Validate(string json, int maxDepth = 16)
		{
			var errors = new List<string>();
			JToken token;

			try
			{
				using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = maxDepth };
				token = JToken.ReadFrom(reader);
			}
			catch (Exception ex)
			{
				errors.Add("not parseable within MaxDepth: " + ex.Message);
				return errors;
			}

			if (token is not JObject root)
			{
				errors.Add("root is not an object");
				return errors;
			}

			ValidateMap(root, "", errors);
			return errors;
		}

		static void ValidateMap(JObject map, string path, List<string> errors)
		{
			foreach (var property in map.Properties())
			{
				var name = path + property.Name;
				if (string.IsNullOrWhiteSpace(property.Name))
					errors.Add($"{name}: blank property name");

				if (property.Value is JObject descriptor)
					ValidateDescriptor(descriptor, name, errors);
				else
					errors.Add($"{name}: described by a {property.Value.Type} rather than by a descriptor object");
			}
		}

		static void ValidateDescriptor(JObject d, string path, List<string> errors)
		{
			foreach (var key in d.Properties().Select(p => p.Name).Where(k => !Keywords.Contains(k)))
				errors.Add($"{path}: unknown keyword '{key}'");

			var type = d["type"]?.Type == JTokenType.String ? (string)d["type"] : null;
			if (type is null || !Types.Contains(type))
			{
				errors.Add($"{path}: missing or unsupported type");
				return;
			}

			foreach (var flag in new[] { "required", "nullable" })
				if (d[flag] != null && d[flag].Type != JTokenType.Boolean)
					errors.Add($"{path}: '{flag}' is not a boolean");

			if (d["description"] != null && d["description"].Type != JTokenType.String)
				errors.Add($"{path}: 'description' is not a string");

			if (d["enum"] != null)
			{
				if (type != "string") errors.Add($"{path}: 'enum' only applies to string");
				if (d["enum"] is not JArray values || values.Count == 0 || values.Any(v => v.Type != JTokenType.String))
					errors.Add($"{path}: 'enum' must be a non-empty array of strings");
			}

			Pair(d, path, "minimum", "maximum", t => t == "number" || t == "integer", wholeCount: false, type, errors);
			Pair(d, path, "minLength", "maxLength", t => t == "string", wholeCount: true, type, errors);
			Pair(d, path, "minItems", "maxItems", t => t == "array", wholeCount: true, type, errors);

			if (type == "array")
			{
				if (d["items"] is JObject items) ValidateDescriptor(items, path + "[]", errors);
				else errors.Add($"{path}: array without an 'items' descriptor");
			}
			else if (d["items"] != null)
				errors.Add($"{path}: 'items' only applies to array");

			if (type == "object")
			{
				if (d["properties"] is JObject properties) ValidateMap(properties, path + ".", errors);
				else errors.Add($"{path}: object without a 'properties' map");
			}
			else if (d["properties"] != null)
				errors.Add($"{path}: 'properties' only applies to object");
		}

		static void Pair(JObject d, string path, string minKey, string maxKey, Func<string, bool> applies, bool wholeCount, string type, List<string> errors)
		{
			var min = d[minKey];
			var max = d[maxKey];
			if (min == null && max == null)
				return;

			if (!applies(type))
			{
				errors.Add($"{path}: '{minKey}'/'{maxKey}' do not apply to {type}");
				return;
			}

			foreach (var (key, value) in new[] { (minKey, min), (maxKey, max) })
			{
				if (value == null) continue;
				if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
					errors.Add($"{path}: '{key}' is not a number");
				else if (wholeCount && (value.Type != JTokenType.Integer || value.Value<long>() < 0))
					errors.Add($"{path}: '{key}' is not a whole number >= 0");
			}

			if (min != null && max != null && min.Value<double>() > max.Value<double>())
				errors.Add($"{path}: '{minKey}' is greater than '{maxKey}'");
		}

		/// <summary>The executor's <c>ExtractJsonObject</c>: first '{' to its balancing '}' by raw brace counting.</summary>
		public static string ExtractJsonObject(string stdout)
		{
			var start = stdout.IndexOf('{');
			if (start < 0) return null;

			var depth = 0;
			for (var i = start; i < stdout.Length; i++)
			{
				if (stdout[i] == '{') depth++;
				else if (stdout[i] == '}' && --depth == 0)
					return stdout.Substring(start, i - start + 1);
			}

			return null;
		}
	}
}
