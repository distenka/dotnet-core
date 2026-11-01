using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Distenka.Testing
{
	/// <summary>
	/// Checks a processor's <c>list</c> output against the platform's descriptor dialect, the way the platform compiles it
	/// into a published input schema (DCS-093, ADR-003 §1.1). The platform <b>fails closed</b>: a schema it cannot compile is
	/// published as unusable and the processor cannot be invoked.
	/// </summary>
	/// <remarks>
	/// A port of <c>PluginInputSchema.TryParse</c> (<c>Distenka.Core.API/Services/PluginInputSchema.cs</c>, platform commit
	/// <see cref="ExecutorRules.SourceCommit"/>; <c>Keywords</c> at lines 133-137). Rules: the root is an object mapping
	/// property name → descriptor (empty is valid); descriptor keywords ⊆ <see cref="Keywords"/>; <c>type</c> required, one of
	/// <see cref="Types"/>; <c>required</c>/<c>nullable</c> booleans; <c>description</c> a string; <c>default</c> accepted and
	/// ignored; <c>minimum</c>/<c>maximum</c> numbers on number/integer, <c>minLength</c>/<c>maxLength</c> on string,
	/// <c>minItems</c>/<c>maxItems</c> on array (whole numbers ≥ 0), min ≤ max; <c>enum</c> a non-empty array of strings on
	/// string; <c>items</c> required for and only for array; <c>properties</c> required for and only for object (may be
	/// empty); property names non-blank and unique; JSON and descriptor nesting within the schema depth limit.
	/// The platform's exact error texts are not reproduced; each finding names the property path and the rule.
	/// </remarks>
	public static class DescriptorSchemaValidator
	{
		/// <summary>The descriptor keywords the platform accepts (<c>PluginInputSchema.Keywords</c>).</summary>
		public static IReadOnlyList<string> Keywords { get; } = new[]
		{
			"type", "required", "nullable", "description", "default", "enum", "minimum", "maximum",
			"minLength", "maxLength", "minItems", "maxItems", "items", "properties",
		};

		/// <summary>The descriptor types the platform accepts (<c>PluginInputType</c>).</summary>
		public static IReadOnlyList<string> Types { get; } = new[] { "string", "number", "integer", "boolean", "object", "array" };

		/// <summary>
		/// Returns why the platform would publish <paramref name="schemaJson"/> as unusable; empty when it is usable.
		/// </summary>
		/// <param name="schemaJson">The schema object, e.g. what <see cref="ExecutorRules.ExtractJsonObject"/> took from <c>list</c>.</param>
		/// <param name="maxDepth">The platform's <c>MaxSchemaDepth</c> (default <see cref="ExecutorRules.DefaultMaxSchemaDepth"/>).</param>
		public static IReadOnlyList<string> Validate(string schemaJson, int maxDepth = ExecutorRules.DefaultMaxSchemaDepth)
		{
			var errors = new List<string>();

			if (schemaJson is null)
			{
				errors.Add("no schema");
				return errors;
			}

			JsonDocument document;
			try
			{
				document = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = maxDepth });
			}
			catch (JsonException ex)
			{
				errors.Add("not parseable within the schema depth limit of " + maxDepth + ": " + ex.Message);
				return errors;
			}

			using (document)
			{
				if (document.RootElement.ValueKind != JsonValueKind.Object)
				{
					errors.Add("the root is not a JSON object");
					return errors;
				}

				ValidateMap(document.RootElement, "", 1, maxDepth, errors);
			}

			return errors;
		}

		static void ValidateMap(JsonElement map, string path, int depth, int maxDepth, List<string> errors)
		{
			var seen = new HashSet<string>(StringComparer.Ordinal);

			foreach (var property in map.EnumerateObject())
			{
				var name = path + property.Name;

				if (string.IsNullOrWhiteSpace(property.Name))
					errors.Add($"'{name}': blank property name");
				else if (!seen.Add(property.Name))
					errors.Add($"'{name}': duplicate property name");

				if (property.Value.ValueKind == JsonValueKind.Object)
					ValidateDescriptor(property.Value, name, depth, maxDepth, errors);
				else
					errors.Add($"'{name}': described by a {property.Value.ValueKind} rather than by a descriptor object");
			}
		}

		static void ValidateDescriptor(JsonElement d, string path, int depth, int maxDepth, List<string> errors)
		{
			if (depth > maxDepth)
			{
				errors.Add($"'{path}': nested deeper than the schema depth limit of {maxDepth}");
				return;
			}

			foreach (var keyword in d.EnumerateObject().Select(p => p.Name).Where(k => !Keywords.Contains(k, StringComparer.Ordinal)))
				errors.Add($"'{path}': unknown keyword '{keyword}'");

			var type = d.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
			if (type is null || !Types.Contains(type, StringComparer.Ordinal))
			{
				errors.Add($"'{path}': missing or unsupported type");
				return;
			}

			foreach (var flag in new[] { "required", "nullable" })
			{
				if (d.TryGetProperty(flag, out var value) && value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
					errors.Add($"'{path}': '{flag}' is not a boolean");
			}

			if (d.TryGetProperty("description", out var description) && description.ValueKind != JsonValueKind.String)
				errors.Add($"'{path}': 'description' is not a string");

			if (d.TryGetProperty("enum", out var permitted))
			{
				if (type != "string")
					errors.Add($"'{path}': 'enum' only applies to string");
				if (permitted.ValueKind != JsonValueKind.Array || permitted.GetArrayLength() == 0 ||
					permitted.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))
					errors.Add($"'{path}': 'enum' must be a non-empty array of strings");
			}

			Pair(d, path, "minimum", "maximum", type == "number" || type == "integer", wholeCount: false, type, errors);
			Pair(d, path, "minLength", "maxLength", type == "string", wholeCount: true, type, errors);
			Pair(d, path, "minItems", "maxItems", type == "array", wholeCount: true, type, errors);

			var hasItems = d.TryGetProperty("items", out var items);
			if (type == "array")
			{
				if (hasItems && items.ValueKind == JsonValueKind.Object)
					ValidateDescriptor(items, path + "[]", depth + 1, maxDepth, errors);
				else
					errors.Add($"'{path}': array without an 'items' descriptor");
			}
			else if (hasItems)
			{
				errors.Add($"'{path}': 'items' only applies to array");
			}

			var hasProperties = d.TryGetProperty("properties", out var properties);
			if (type == "object")
			{
				if (hasProperties && properties.ValueKind == JsonValueKind.Object)
					ValidateMap(properties, path + ".", depth + 1, maxDepth, errors);
				else
					errors.Add($"'{path}': object without a 'properties' map");
			}
			else if (hasProperties)
			{
				errors.Add($"'{path}': 'properties' only applies to object");
			}
		}

		static void Pair(JsonElement d, string path, string minKey, string maxKey, bool applies, bool wholeCount, string type, List<string> errors)
		{
			var hasMin = d.TryGetProperty(minKey, out var min);
			var hasMax = d.TryGetProperty(maxKey, out var max);
			if (!hasMin && !hasMax)
				return;

			if (!applies)
			{
				errors.Add($"'{path}': '{minKey}'/'{maxKey}' do not apply to {type}");
				return;
			}

			var valid = true;
			foreach (var (key, present, value) in new[] { (minKey, hasMin, min), (maxKey, hasMax, max) })
			{
				if (!present)
					continue;

				if (value.ValueKind != JsonValueKind.Number)
				{
					errors.Add($"'{path}': '{key}' is not a number");
					valid = false;
				}
				else if (wholeCount && (!value.TryGetInt64(out var count) || count < 0))
				{
					errors.Add($"'{path}': '{key}' is not a whole number >= 0");
					valid = false;
				}
			}

			if (valid && hasMin && hasMax && min.GetDouble() > max.GetDouble())
				errors.Add($"'{path}': '{minKey}' is greater than '{maxKey}'");
		}
	}
}
