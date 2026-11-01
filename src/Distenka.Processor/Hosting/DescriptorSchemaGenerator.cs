using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Distenka.Hosting
{
	/// <summary>The outcome of describing a package's processes (ADR-003 §2.1).</summary>
	internal sealed class DescriptorSchemaResult
	{
		/// <summary>The descriptor map, one compact line, braces in strings escaped; <c>null</c> when <see cref="Errors"/> is not empty.</summary>
		public string Json { get; init; }

		/// <summary>The descriptor map as a tree (for <c>list --verbose</c> and tests); <c>null</c> on failure.</summary>
		public JObject Schema { get; init; }

		/// <summary>Reasons the package cannot be described, each <c>&lt;member path&gt;: &lt;reason&gt;</c>.</summary>
		public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

		/// <summary>Diagnostics that do not prevent the description (e.g. invalid processor types skipped).</summary>
		public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

		public bool Success => Errors.Count == 0;
	}

	/// <summary>
	/// Generates the ACC-36 descriptor-form input schema printed by <c>list</c> from the processes' <see cref="Config"/> types
	/// (ADR-003 mapping and rejection rules; ADR-002 §2.3 process descriptor and union rules). Reflection only: no config,
	/// processor or dependency is constructed (ADR-003 §2.1 rule 4).
	/// </summary>
	internal static class DescriptorSchemaGenerator
	{
		/// <summary>The platform's default <c>MaxSchemaDepth</c> (ADR-003 §2.1 rule 6).</summary>
		public const int MaxDepth = 16;

		const string ProcessPropertyName = "process";

		static readonly IContractResolver Resolver = ConfigWriter.Settings.ContractResolver;

		public static DescriptorSchemaResult Generate(IEnumerable<ProcessInfo> processes)
		{
			var errors = new List<string>();
			var warnings = new List<string>();

			var all = (processes ?? Enumerable.Empty<ProcessInfo>()).ToList();

			foreach (var invalid in all.Where(p => !p.IsValid).OrderBy(p => p.ProcessType.FullName, StringComparer.Ordinal))
				warnings.Add($"{invalid.ProcessType.FullName}: not a runnable process ({invalid.Errors}); skipped");

			var valid = all.Where(p => p.IsValid).OrderBy(p => p.ProcessType.FullName, StringComparer.Ordinal).ToList();

			if (valid.Count == 0)
				errors.Add("package: contains no runnable process");

			foreach (var duplicate in valid.GroupBy(p => p.ProcessType.FullName, StringComparer.Ordinal).Where(g => g.Count() > 1))
				errors.Add($"process: {duplicate.Key} is defined by more than one assembly ({string.Join(", ", duplicate.Select(p => p.ProcessType.Assembly.GetName().Name))})");

			if (errors.Count > 0)
				return new DescriptorSchemaResult { Errors = errors, Warnings = warnings };

			return Generate(valid.Select(p => (p.ProcessType.FullName, p.ConfigType)), warnings);
		}

		/// <summary>
		/// Describes a process set given as (canonical process name, configuration type) pairs — the core of
		/// <see cref="Generate(IEnumerable{ProcessInfo})"/>, usable without processor types (tests).
		/// </summary>
		internal static DescriptorSchemaResult Generate(IEnumerable<(string Name, Type ConfigType)> processSet, IReadOnlyList<string> warnings = null)
		{
			warnings ??= Array.Empty<string>();
			var errors = new List<string>();
			var valid = processSet.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

			if (valid.Count == 0)
				return new DescriptorSchemaResult { Errors = new[] { "package: contains no runnable process" }, Warnings = warnings };

			var names = valid.Select(p => p.Name).ToList();

			var root = new List<KeyValuePair<string, Merged>>();
			var byName = new Dictionary<string, Merged>(StringComparer.Ordinal);

			foreach (var process in valid)
			{
				var properties = DescribeMembers(process.ConfigType, "", excludeBaseConfig: true, depth: 1, new Stack<Type>(), errors);
				var processName = process.Name;

				foreach (var (name, descriptor) in properties)
				{
					if (name == ProcessPropertyName)
					{
						errors.Add($"{name}: {process.ConfigType.FullName} (process {processName}) declares a member serialized as 'process', which is reserved for the process name");
						continue;
					}

					if (byName.TryGetValue(name, out var existing))
					{
						if (!Compatible(existing.Descriptor, descriptor))
						{
							errors.Add($"{name}: declared incompatibly by {existing.Processes[0]} ({Shape(existing.Descriptor)}) and {processName} ({Shape(descriptor)})");
							continue;
						}

						existing.Processes.Add(processName);
						existing.RequiredBy += descriptor.Value<bool?>("required") == true ? 1 : 0;

						if (existing.Descriptor["description"] == null && descriptor["description"] != null)
							existing.Descriptor["description"] = descriptor["description"].DeepClone();
					}
					else
					{
						var merged = new Merged
						{
							Descriptor = descriptor,
							Processes = new List<string> { processName },
							RequiredBy = descriptor.Value<bool?>("required") == true ? 1 : 0
						};
						byName.Add(name, merged);
						root.Add(new KeyValuePair<string, Merged>(name, merged));
					}
				}
			}

			if (errors.Count > 0)
				return new DescriptorSchemaResult { Errors = errors, Warnings = warnings };

			var schema = new JObject
			{
				[ProcessPropertyName] = new JObject
				{
					["type"] = "string",
					["required"] = valid.Count > 1,
					["enum"] = new JArray(names),
					["description"] = $"Process to run. One of: {string.Join(", ", names)}."
				}
			};

			foreach (var (name, merged) in root)
			{
				// ADR-002 §2.3 rule 4: required in the union only when every process declares and requires it.
				var descriptor = merged.Descriptor;
				descriptor.Remove("required");
				if (merged.RequiredBy == valid.Count)
					InsertAfterType(descriptor, "required", true);

				schema[name] = descriptor;
			}

			var depth = Depth(schema);
			if (depth > MaxDepth)
				return new DescriptorSchemaResult
				{
					Errors = new[] { $"package: the input schema nests {depth} levels, more than the platform's limit of {MaxDepth}" },
					Warnings = warnings
				};

			return new DescriptorSchemaResult { Json = Write(schema), Schema = schema, Warnings = warnings };
		}

		sealed class Merged
		{
			public JObject Descriptor;
			public List<string> Processes;
			public int RequiredBy;
		}

		// ── members ────────────────────────────────────────────────────────────────────────────────

		static List<(string Name, JObject Descriptor)> DescribeMembers(Type type, string pathPrefix, bool excludeBaseConfig, int depth, Stack<Type> visiting, List<string> errors)
		{
			var result = new List<(string, JObject)>();
			JsonObjectContract contract;

			try
			{
				contract = Resolver.ResolveContract(type) as JsonObjectContract;
			}
			catch (Exception ex) when (ex is JsonSerializationException || ex is InvalidOperationException)
			{
				// e.g. two members with the same serialized name (ADR-003 §2.2).
				errors.Add($"{Path(pathPrefix, type.Name)}: {ex.Message}");
				return result;
			}

			if (contract is null)
			{
				errors.Add($"{Path(pathPrefix, type.Name)}: {TypeName(type)} is not an object type");
				return result;
			}

			foreach (var property in contract.Properties)
			{
				if (property.Ignored || !property.Writable)
					continue;

				if (excludeBaseConfig && property.DeclaringType == typeof(Config))
					continue;

				var path = Path(pathPrefix, property.PropertyName);
				var descriptor = DescribeMember(property, path, depth, visiting, errors);

				if (descriptor != null)
					result.Add((property.PropertyName, descriptor));
			}

			return result;
		}

		static JObject DescribeMember(JsonProperty property, string path, int depth, Stack<Type> visiting, List<string> errors)
		{
			if (property.Converter != null)
			{
				errors.Add($"{path}: [JsonConverter] members have a custom wire shape that cannot be described");
				return null;
			}

			if (property.TypeNameHandling.HasValue && property.TypeNameHandling.Value != TypeNameHandling.None)
			{
				errors.Add($"{path}: TypeNameHandling (polymorphic) members cannot be described");
				return null;
			}

			var memberType = property.PropertyType;
			var errorCount = errors.Count;
			var descriptor = DescribeType(memberType, path, depth, visiting, errors);

			if (descriptor == null || errors.Count > errorCount)
				return null;

			IList<Attribute> attributes = property.AttributeProvider?.GetAttributes(true) ?? (IList<Attribute>)Array.Empty<Attribute>();
			var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;

			// nullable
			var nullable = Nullable.GetUnderlyingType(memberType) != null || IsNullableReference(property);

			// required
			var required = attributes.OfType<RequiredAttribute>().Any() ||
				property.Required is Required.Always or Required.AllowNull or Required.DisallowNull;

			if (property.Required == Required.AllowNull)
				nullable = true;

			if (!ApplyAttributes(descriptor, underlying, attributes, path, errors))
				return null;

			var requiredAttribute = attributes.OfType<RequiredAttribute>().FirstOrDefault();
			if (requiredAttribute != null && !requiredAttribute.AllowEmptyStrings && (string)descriptor["type"] == "string")
			{
				var min = descriptor.Value<long?>("minLength");
				if (min is null || min < 1)
					Set(descriptor, "minLength", 1L);
			}

			if (required)
				InsertAfterType(descriptor, "required", true);

			if (nullable)
				InsertAfterType(descriptor, "nullable", true);

			var description = attributes.OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
			if (!string.IsNullOrEmpty(description))
				descriptor["description"] = description;

			return descriptor;
		}

		static bool IsNullableReference(JsonProperty property)
		{
			if (property.PropertyType.IsValueType || property.DeclaringType is null)
				return false;

			try
			{
				var member = property.DeclaringType.GetMember(property.UnderlyingName,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();

				var context = new NullabilityInfoContext();
				var info = member switch
				{
					PropertyInfo p => context.Create(p),
					FieldInfo f => context.Create(f),
					_ => null
				};

				// Oblivious (no nullable annotations) is treated as non-nullable (ADR-003 §2.3).
				return info?.WriteState == NullabilityState.Nullable;
			}
			catch
			{
				return false;
			}
		}

		// ── types ──────────────────────────────────────────────────────────────────────────────────

		static readonly Dictionary<Type, (object Min, object Max)> IntegerRanges = new()
		{
			[typeof(sbyte)] = (sbyte.MinValue, sbyte.MaxValue),
			[typeof(byte)] = (byte.MinValue, byte.MaxValue),
			[typeof(short)] = (short.MinValue, short.MaxValue),
			[typeof(ushort)] = (ushort.MinValue, ushort.MaxValue),
			[typeof(int)] = (int.MinValue, int.MaxValue),
			[typeof(uint)] = (uint.MinValue, uint.MaxValue),
		};

		static readonly Type[] StringLikeTypes =
		{
			typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Uri), typeof(byte[])
		};

		static readonly Type[] CollectionDefinitions =
		{
			typeof(List<>), typeof(IList<>), typeof(ICollection<>), typeof(IEnumerable<>), typeof(IReadOnlyList<>),
			typeof(IReadOnlyCollection<>), typeof(HashSet<>), typeof(ISet<>)
		};

		static JObject DescribeType(Type type, string path, int depth, Stack<Type> visiting, List<string> errors)
		{
			var underlying = Nullable.GetUnderlyingType(type) ?? type;

			if (StringLikeTypes.Contains(underlying))
				return new JObject { ["type"] = "string" };

			if (underlying == typeof(char))
				return new JObject { ["type"] = "string", ["minLength"] = 1L, ["maxLength"] = 1L };

			if (underlying == typeof(Guid))
				return new JObject { ["type"] = "string", ["minLength"] = 32L, ["maxLength"] = 38L };

			if (underlying == typeof(bool))
				return new JObject { ["type"] = "boolean" };

			if (IntegerRanges.TryGetValue(underlying, out var range))
				return new JObject { ["type"] = "integer", ["minimum"] = Convert.ToInt64(range.Min), ["maximum"] = Convert.ToInt64(range.Max) };

			if (underlying == typeof(long))
				return new JObject { ["type"] = "integer" };

			if (underlying == typeof(ulong))
				return new JObject { ["type"] = "integer", ["minimum"] = 0L };

			if (underlying == typeof(float))
				return new JObject { ["type"] = "number", ["minimum"] = (double)float.MinValue, ["maximum"] = (double)float.MaxValue };

			if (underlying == typeof(decimal))
				return new JObject { ["type"] = "number", ["minimum"] = decimal.MinValue, ["maximum"] = decimal.MaxValue };

			if (underlying == typeof(double))
				return new JObject { ["type"] = "number" };

			if (underlying.IsEnum)
				return DescribeEnum(underlying, path, errors);

			if (IsUnsupportedDynamic(underlying))
			{
				errors.Add($"{path}: {TypeName(underlying)} is not expressible in the Distenka input schema" +
					(IsDictionary(underlying) ? " (use a nested class or an array of key/value objects)" : ""));
				return null;
			}

			var element = CollectionElement(underlying);
			if (element != null)
			{
				var items = DescribeType(element, path + "[]", depth + 1, visiting, errors);
				return items == null ? null : new JObject { ["type"] = "array", ["items"] = items };
			}

			if (typeof(IEnumerable).IsAssignableFrom(underlying))
			{
				errors.Add($"{path}: collection type {TypeName(underlying)} is not supported (use an array or List<T>)");
				return null;
			}

			if (underlying.IsInterface || underlying.IsAbstract)
			{
				errors.Add($"{path}: {TypeName(underlying)} is an interface or abstract type; its wire shape cannot be described");
				return null;
			}

			if (underlying.GetCustomAttribute<JsonConverterAttribute>(true) != null)
			{
				errors.Add($"{path}: {TypeName(underlying)} has a [JsonConverter]; its wire shape cannot be described");
				return null;
			}

			if (underlying.IsPrimitive || underlying == typeof(IntPtr) || underlying == typeof(UIntPtr))
			{
				errors.Add($"{path}: {TypeName(underlying)} is not supported");
				return null;
			}

			if (visiting.Contains(underlying))
			{
				errors.Add($"{path}: {TypeName(underlying)} contains itself (cycle)");
				return null;
			}

			visiting.Push(underlying);
			try
			{
				var errorCount = errors.Count;
				var members = DescribeMembers(underlying, path, excludeBaseConfig: false, depth + 1, visiting, errors);
				if (errors.Count > errorCount)
					return null;

				var properties = new JObject();
				foreach (var (name, descriptor) in members)
					properties[name] = descriptor;

				return new JObject { ["type"] = "object", ["properties"] = properties };
			}
			finally
			{
				visiting.Pop();
			}
		}

		static JObject DescribeEnum(Type type, string path, List<string> errors)
		{
			if (type.GetCustomAttribute<FlagsAttribute>() != null)
			{
				errors.Add($"{path}: [Flags] enum {TypeName(type)} is not expressible (use an array of a non-flags enum)");
				return null;
			}

			var values = type.GetFields(BindingFlags.Public | BindingFlags.Static)
				.OrderBy(f => f.MetadataToken)
				.Select(f => f.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? f.Name)
				.ToList();

			if (values.Count == 0)
			{
				errors.Add($"{path}: enum {TypeName(type)} has no members");
				return null;
			}

			return new JObject { ["type"] = "string", ["enum"] = new JArray(values) };
		}

		static bool IsDictionary(Type type) =>
			typeof(IDictionary).IsAssignableFrom(type) ||
			ImplementsGeneric(type, typeof(IDictionary<,>)) ||
			ImplementsGeneric(type, typeof(IReadOnlyDictionary<,>));

		static bool IsUnsupportedDynamic(Type type) =>
			type == typeof(object) ||
			IsDictionary(type) ||
			typeof(JToken).IsAssignableFrom(type) ||
			type.FullName == "System.Dynamic.ExpandoObject" ||
			(type.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) ?? false);

		static Type CollectionElement(Type type)
		{
			if (type.IsArray)
				return type.GetArrayRank() == 1 ? type.GetElementType() : null;

			if (type.IsGenericType && CollectionDefinitions.Contains(type.GetGenericTypeDefinition()))
				return type.GetGenericArguments()[0];

			return null;
		}

		static bool ImplementsGeneric(Type type, Type definition) =>
			(type.IsGenericType && type.GetGenericTypeDefinition() == definition) ||
			type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition);

		// ── attributes ─────────────────────────────────────────────────────────────────────────────

		static bool ApplyAttributes(JObject descriptor, Type type, IList<Attribute> attributes, string path, List<string> errors)
		{
			var kind = (string)descriptor["type"];
			var ok = true;

			void Reject(Attribute attribute, string reason)
			{
				errors.Add($"{path}: [{attribute.GetType().Name.Replace("Attribute", "")}] {reason}");
				ok = false;
			}

			foreach (var attribute in attributes)
			{
				switch (attribute)
				{
					case RangeAttribute r:
						if (kind != "integer" && kind != "number")
						{
							Reject(r, $"does not apply to a {kind}");
							break;
						}

						if (!TryReadBound(r.Minimum, r.OperandType, out var min) || !TryReadBound(r.Maximum, r.OperandType, out var max))
						{
							Reject(r, "has bounds that are not numbers");
							break;
						}

						if (kind == "number" && (r.MinimumIsExclusive || r.MaximumIsExclusive))
						{
							Reject(r, "exclusive bounds are not expressible on a number");
							break;
						}

						if (kind == "integer")
						{
							min = r.MinimumIsExclusive ? Math.Floor(min) + 1 : Math.Ceiling(min);
							max = r.MaximumIsExclusive ? Math.Ceiling(max) - 1 : Math.Floor(max);
						}

						Intersect(descriptor, kind, min, max);
						break;

					case StringLengthAttribute s:
						if (kind != "string")
						{
							Reject(s, $"does not apply to a {kind}");
							break;
						}
						Set(descriptor, "maxLength", (long)s.MaximumLength);
						if (s.MinimumLength > 0)
							Set(descriptor, "minLength", (long)s.MinimumLength);
						break;

					case MinLengthAttribute m:
						if (!ApplyLength(descriptor, kind, minimum: m.Length, maximum: null))
							Reject(m, $"does not apply to a {kind}");
						break;

					case MaxLengthAttribute m:
						if (m.Length < 0)
							break; // [MaxLength] without a length means "the maximum allowed": no constraint.
						if (!ApplyLength(descriptor, kind, minimum: null, maximum: m.Length))
							Reject(m, $"does not apply to a {kind}");
						break;

					case LengthAttribute l:
						if (!ApplyLength(descriptor, kind, minimum: l.MinimumLength, maximum: l.MaximumLength))
							Reject(l, $"does not apply to a {kind}");
						break;

					case AllowedValuesAttribute a:
						if (kind != "string" || a.Values is null || a.Values.Length == 0 || a.Values.Any(v => v is not string))
						{
							Reject(a, "applies only to a string member with string values");
							break;
						}
						descriptor["enum"] = new JArray(a.Values.Cast<string>());
						break;
				}
			}

			return ok;
		}

		static bool ApplyLength(JObject descriptor, string kind, int? minimum, int? maximum)
		{
			var (minKey, maxKey) = kind switch
			{
				"string" => ("minLength", "maxLength"),
				"array" => ("minItems", "maxItems"),
				_ => ((string)null, (string)null)
			};

			if (minKey is null)
				return false;

			if (minimum.HasValue)
				Set(descriptor, minKey, (long)minimum.Value);
			if (maximum.HasValue)
				Set(descriptor, maxKey, (long)maximum.Value);

			return true;
		}

		static bool TryReadBound(object value, Type operandType, out double result)
		{
			result = 0;
			switch (value)
			{
				case null:
					return false;
				case string s:
					return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
				case IConvertible c:
					try { result = c.ToDouble(CultureInfo.InvariantCulture); return true; }
					catch { return false; }
				default:
					return false;
			}
		}

		static void Intersect(JObject descriptor, string kind, double min, double max)
		{
			var currentMin = descriptor["minimum"];
			var currentMax = descriptor["maximum"];

			if (currentMin == null || min > currentMin.Value<double>())
				Set(descriptor, "minimum", kind == "integer" ? (JToken)(long)min : min);
			if (currentMax == null || max < currentMax.Value<double>())
				Set(descriptor, "maximum", kind == "integer" ? (JToken)(long)max : max);
		}

		// ── merge, order, output ───────────────────────────────────────────────────────────────────

		static bool Compatible(JObject a, JObject b) => JToken.DeepEquals(Comparable(a), Comparable(b));

		static JObject Comparable(JObject descriptor)
		{
			var copy = (JObject)descriptor.DeepClone();
			copy.Remove("description");
			copy.Remove("required");
			return copy;
		}

		static string Shape(JObject descriptor) => Comparable(descriptor).ToString(Formatting.None);

		/// <summary>Keyword order of every descriptor: type, required, nullable, enum, bounds, items, properties, description.</summary>
		static readonly string[] KeywordOrder =
		{
			"type", "required", "nullable", "enum", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "items", "properties", "description"
		};

		static void Set(JObject descriptor, string key, JToken value)
		{
			descriptor[key] = value;
			Reorder(descriptor);
		}

		static void InsertAfterType(JObject descriptor, string key, JToken value)
		{
			descriptor[key] = value;
			Reorder(descriptor);
		}

		static void Reorder(JObject descriptor)
		{
			var ordered = descriptor.Properties()
				.OrderBy(p => Array.IndexOf(KeywordOrder, p.Name) is var i && i >= 0 ? i : int.MaxValue)
				.ToList();

			descriptor.RemoveAll();
			foreach (var p in ordered)
				descriptor.Add(p);
		}

		static int Depth(JToken token) => token switch
		{
			JObject o => 1 + (o.Properties().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max()),
			JArray a => 1 + (a.Select(Depth).DefaultIfEmpty(0).Max()),
			_ => 0
		};

		/// <summary>
		/// Compact JSON with every <c>{</c>/<c>}</c> inside a string written as <c>{</c>/<c>}</c>, so the executor's
		/// raw brace-counting extractor stays balanced (ADR-003 §2.1 rule 2).
		/// </summary>
		public static string Write(JToken token)
		{
			var sb = new StringBuilder();
			WriteToken(sb, token);
			return sb.ToString();
		}

		static void WriteToken(StringBuilder sb, JToken token)
		{
			switch (token)
			{
				case JObject o:
					sb.Append('{');
					var first = true;
					foreach (var p in o.Properties())
					{
						if (!first) sb.Append(',');
						first = false;
						sb.Append(EscapeString(p.Name)).Append(':');
						WriteToken(sb, p.Value);
					}
					sb.Append('}');
					break;

				case JArray a:
					sb.Append('[');
					for (var i = 0; i < a.Count; i++)
					{
						if (i > 0) sb.Append(',');
						WriteToken(sb, a[i]);
					}
					sb.Append(']');
					break;

				case JValue v when v.Type == JTokenType.String:
					sb.Append(EscapeString((string)v.Value));
					break;

				default:
					sb.Append(token.ToString(Formatting.None));
					break;
			}
		}

		static string EscapeString(string value) =>
			JsonConvert.ToString(value).Replace("{", "\\u007B").Replace("}", "\\u007D");

		static string Path(string prefix, string name) => string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;

		static string TypeName(Type type)
		{
			if (!type.IsGenericType)
				return type.Name;

			var name = type.Name.Substring(0, type.Name.IndexOf('`'));
			return $"{name}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>";
		}
	}
}
