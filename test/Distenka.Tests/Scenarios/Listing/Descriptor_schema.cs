using FluentAssertions;
using Distenka.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Xunit;

namespace Distenka.Tests.Scenarios.Listing
{
	// Config types only (no processors): processors here would be discovered by the other tests' hosts.
	public enum ListColor { Red, Green, Blue }
	public enum ListTagged { [EnumMember(Value = "first-one")] First, Second }
	[Flags] public enum ListPerms { Read = 1, Write = 2 }
	public enum ListEmpty { }

	public class ListNested
	{
		public string Name { get; set; }
		[Required] public int Count { get; set; }
	}

	public class AllTypesConfig : Config
	{
		public string S { get; set; }
		public char C { get; set; }
		public Guid G { get; set; }
		public DateTime D { get; set; }
		public byte[] Bytes { get; set; }
		public bool B { get; set; }
		public byte U8 { get; set; }
		public short I16 { get; set; }
		public int I32 { get; set; }
		public uint U32 { get; set; }
		public long I64 { get; set; }
		public ulong U64 { get; set; }
		public float F { get; set; }
		public double Dbl { get; set; }
		public decimal Dec { get; set; }
		public int? MaybeInt { get; set; }
		public ListColor Color { get; set; }
		public ListTagged Tagged { get; set; }
		public int[] Ints { get; set; }
		public List<string> Names { get; set; }
		public ListNested Child { get; set; }
		public ListNested[] Children { get; set; }
	}

	public class AttributesConfig : Config
	{
		[Required, Description("The name {with braces}")] public string Name { get; set; }
		[Required(AllowEmptyStrings = true)] public string AnyText { get; set; }
		[Range(1, 10)] public int Small { get; set; }
		[Range(0.5, 2.5)] public double Ratio { get; set; }
		[Range(typeof(int), "0", "5", MinimumIsExclusive = true)] public int Exclusive { get; set; }
		[StringLength(20, MinimumLength = 3)] public string Code { get; set; }
		[MinLength(2), MaxLength(4)] public string[] Tags { get; set; }
		[Length(1, 3)] public string Short { get; set; }
		[AllowedValues("a", "b")] public string Mode { get; set; }
		[JsonProperty("renamed")] public int Original { get; set; }
		[JsonIgnore] public int Hidden { get; set; }
		[JsonProperty(Required = Required.AllowNull)] public string Nullish { get; set; }
		[DefaultValue(5)] public int WithDefault { get; set; }
		[RegularExpression("x+")] public string Pattern { get; set; }
		public int ReadOnly => 1;
	}

	public class DictionaryConfig : Config { public Dictionary<string, string> Lookup { get; set; } }
	public class ObjectConfig : Config { public object Anything { get; set; } }
	public class JTokenConfig : Config { public JObject Payload { get; set; } }
	public class InterfaceConfig : Config { public IComparable Thing { get; set; } }
	public class FlagsConfig : Config { public ListPerms Perms { get; set; } }
	public class EmptyEnumConfig : Config { public ListEmpty Nothing { get; set; } }
	public class ListNode { public ListNode Next { get; set; } }
	public class CycleConfig : Config { public ListNode Root { get; set; } }
	public class MisfitConfig : Config { [Range(1, 2)] public string Text { get; set; } }
	public class ExclusiveNumberConfig : Config { [Range(0.0, 1.0, MinimumIsExclusive = true)] public double Fraction { get; set; } }
	public class ProcessMemberConfig : Config { public new string Process { get; set; } }
	public class DuplicateNameConfig : Config { [JsonProperty("dup")] public int A { get; set; } [JsonProperty("dup")] public int B { get; set; } }
	public class ConverterConfig : Config { [JsonConverter(typeof(StringEnumConverter))] public ListColor Color { get; set; } }

	public class Deep8 { public string Leaf { get; set; } }
	public class Deep7 { public Deep8 Next { get; set; } }
	public class Deep6 { public Deep7 Next { get; set; } }
	public class Deep5 { public Deep6 Next { get; set; } }
	public class Deep4 { public Deep5 Next { get; set; } }
	public class Deep3 { public Deep4 Next { get; set; } }
	public class Deep2 { public Deep3 Next { get; set; } }
	public class Deep1 { public Deep2 Next { get; set; } }
	public class TooDeepConfig : Config { public Deep1 Start { get; set; } }

	public class UnionAConfig : Config { [Required] public int Shared { get; set; } public string OnlyA { get; set; } public int Described { get; set; } }
	public class UnionBConfig : Config { [Required] public int Shared { get; set; } [Required] public string OnlyB { get; set; } [Description("From B")] public int Described { get; set; } }
	public class ConflictAConfig : Config { public int Value { get; set; } }
	public class ConflictBConfig : Config { public string Value { get; set; } }

	public abstract class AbstractListedProcessor : Processor<Config>
	{
		protected AbstractListedProcessor(Config config) : base(config) { }
	}

	/// <summary>DCS-043 / ADR-003 §2.3–2.5, ADR-002 §2.3: the CLR → descriptor mapping, rejection and union rules.</summary>
	public class Descriptor_schema
	{
		static DescriptorSchemaResult Single(Type config, string name = "Acme.Job") =>
			DescriptorSchemaGenerator.Generate(new[] { (name, config) });

		static string Descriptor(DescriptorSchemaResult result, string property) =>
			DescriptorSchemaGenerator.Write(result.Schema[property]);

		[Theory]
		[InlineData("s", "{\"type\":\"string\"}")]
		[InlineData("c", "{\"type\":\"string\",\"minLength\":1,\"maxLength\":1}")]
		[InlineData("g", "{\"type\":\"string\",\"minLength\":32,\"maxLength\":38}")]
		[InlineData("d", "{\"type\":\"string\"}")]
		[InlineData("bytes", "{\"type\":\"string\"}")]
		[InlineData("b", "{\"type\":\"boolean\"}")]
		[InlineData("u8", "{\"type\":\"integer\",\"minimum\":0,\"maximum\":255}")]
		[InlineData("i16", "{\"type\":\"integer\",\"minimum\":-32768,\"maximum\":32767}")]
		[InlineData("i32", "{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}")]
		[InlineData("u32", "{\"type\":\"integer\",\"minimum\":0,\"maximum\":4294967295}")]
		[InlineData("i64", "{\"type\":\"integer\"}")]
		[InlineData("u64", "{\"type\":\"integer\",\"minimum\":0}")]
		[InlineData("dbl", "{\"type\":\"number\"}")]
		[InlineData("dec", "{\"type\":\"number\",\"minimum\":-79228162514264337593543950335.0,\"maximum\":79228162514264337593543950335.0}")]
		[InlineData("maybeInt", "{\"type\":\"integer\",\"nullable\":true,\"minimum\":-2147483648,\"maximum\":2147483647}")]
		[InlineData("color", "{\"type\":\"string\",\"enum\":[\"Red\",\"Green\",\"Blue\"]}")]
		[InlineData("tagged", "{\"type\":\"string\",\"enum\":[\"first-one\",\"Second\"]}")]
		[InlineData("ints", "{\"type\":\"array\",\"items\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}}")]
		[InlineData("names", "{\"type\":\"array\",\"items\":{\"type\":\"string\"}}")]
		[InlineData("child", "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"count\":{\"type\":\"integer\",\"required\":true,\"minimum\":-2147483648,\"maximum\":2147483647}}}")]
		[InlineData("children", "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"count\":{\"type\":\"integer\",\"required\":true,\"minimum\":-2147483648,\"maximum\":2147483647}}}}")]
		public void each_clr_type_maps_as_specified(string property, string expected)
		{
			var result = Single(typeof(AllTypesConfig));

			result.Errors.Should().BeEmpty();
			Descriptor(result, property).Should().Be(expected);
		}

		[Fact]
		public void float_maps_to_number_with_its_clr_range()
		{
			var f = Single(typeof(AllTypesConfig)).Schema["f"];

			((string)f["type"]).Should().Be("number");
			f.Value<double>("minimum").Should().Be(float.MinValue);
			f.Value<double>("maximum").Should().Be(float.MaxValue);
		}

		[Theory]
		[InlineData("name", "{\"type\":\"string\",\"required\":true,\"minLength\":1,\"description\":\"The name \\u007Bwith braces\\u007D\"}")]
		[InlineData("anyText", "{\"type\":\"string\",\"required\":true}")]
		[InlineData("small", "{\"type\":\"integer\",\"minimum\":1,\"maximum\":10}")]
		[InlineData("ratio", "{\"type\":\"number\",\"minimum\":0.5,\"maximum\":2.5}")]
		[InlineData("exclusive", "{\"type\":\"integer\",\"minimum\":1,\"maximum\":5}")]
		[InlineData("code", "{\"type\":\"string\",\"minLength\":3,\"maxLength\":20}")]
		[InlineData("tags", "{\"type\":\"array\",\"minItems\":2,\"maxItems\":4,\"items\":{\"type\":\"string\"}}")]
		[InlineData("short", "{\"type\":\"string\",\"minLength\":1,\"maxLength\":3}")]
		[InlineData("mode", "{\"type\":\"string\",\"enum\":[\"a\",\"b\"]}")]
		[InlineData("renamed", "{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}")]
		[InlineData("nullish", "{\"type\":\"string\",\"required\":true,\"nullable\":true}")]
		[InlineData("withDefault", "{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647}")]
		[InlineData("pattern", "{\"type\":\"string\"}")]
		public void attributes_map_as_specified(string property, string expected)
		{
			var result = Single(typeof(AttributesConfig));

			result.Errors.Should().BeEmpty();
			Descriptor(result, property).Should().Be(expected);
		}

		[Fact]
		public void ignored_read_only_and_base_config_members_are_not_emitted()
		{
			var schema = Single(typeof(AttributesConfig)).Schema;

			schema.Properties().Select(p => p.Name).Should().NotContain(new[] { "hidden", "original", "readOnly", "execution" });
			schema.Properties().First().Name.Should().Be("process");
		}

		[Fact]
		public void no_default_keyword_and_only_dialect_keywords_are_emitted()
		{
			foreach (var config in new[] { typeof(AllTypesConfig), typeof(AttributesConfig) })
			{
				var result = Single(config);

				result.Json.Should().NotContain("\"default\"");
				DescriptorDialect.Validate(result.Json).Should().BeEmpty();
			}
		}

		[Fact]
		public void braces_in_strings_survive_the_executors_raw_brace_counting()
		{
			var json = Single(typeof(AttributesConfig)).Json;

			json.Should().Contain("\\u007Bwith braces\\u007D");
			var extracted = DescriptorDialect.ExtractJsonObject(json + "\n");
			extracted.Should().Be(json);
			JObject.Parse(extracted)["name"]["description"].Value<string>().Should().Be("The name {with braces}");
		}

		[Fact]
		public void output_is_one_line_and_byte_identical_across_runs()
		{
			var first = Single(typeof(AllTypesConfig)).Json;
			var second = Single(typeof(AllTypesConfig)).Json;

			first.Should().Be(second);
			first.Should().NotContainAny("\n", "\r");
		}

		[Theory]
		[InlineData(typeof(DictionaryConfig), "lookup", "not expressible")]
		[InlineData(typeof(ObjectConfig), "anything", "not expressible")]
		[InlineData(typeof(JTokenConfig), "payload", "not expressible")]
		[InlineData(typeof(InterfaceConfig), "thing", "interface or abstract")]
		[InlineData(typeof(FlagsConfig), "perms", "[Flags]")]
		[InlineData(typeof(EmptyEnumConfig), "nothing", "no members")]
		[InlineData(typeof(CycleConfig), "root.next", "cycle")]
		[InlineData(typeof(MisfitConfig), "text", "[Range] does not apply to a string")]
		[InlineData(typeof(ExclusiveNumberConfig), "fraction", "exclusive bounds")]
		[InlineData(typeof(ProcessMemberConfig), "process", "reserved")]
		[InlineData(typeof(DuplicateNameConfig), "DuplicateNameConfig", "dup")]
		[InlineData(typeof(ConverterConfig), "color", "[JsonConverter]")]
		[InlineData(typeof(TooDeepConfig), "package", "more than the platform's limit of 16")]
		public void unsupported_shapes_are_rejected_naming_the_member(Type config, string path, string reason)
		{
			var result = Single(config);

			result.Success.Should().BeFalse();
			result.Json.Should().BeNull();
			result.Errors.Should().Contain(e => e.StartsWith(path) && e.Contains(reason), string.Join(Environment.NewLine, result.Errors));
		}

		[Fact]
		public void a_single_process_is_optional_with_a_one_element_enum()
		{
			Descriptor(Single(typeof(AllTypesConfig), "Acme.Only"), "process").Should().Be(
				"{\"type\":\"string\",\"required\":false,\"enum\":[\"Acme.Only\"],\"description\":\"Process to run. One of: Acme.Only.\"}");
		}

		[Fact]
		public void several_processes_publish_a_required_process_and_the_union_of_their_properties()
		{
			// Given in reverse order: the enum and the property order follow ordinal canonical-name order.
			var result = DescriptorSchemaGenerator.Generate(new[] { ("Acme.B", typeof(UnionBConfig)), ("Acme.A", typeof(UnionAConfig)) });

			result.Errors.Should().BeEmpty();
			result.Json.Should().Be(
				"{\"process\":{\"type\":\"string\",\"required\":true,\"enum\":[\"Acme.A\",\"Acme.B\"],\"description\":\"Process to run. One of: Acme.A, Acme.B.\"}," +
				"\"shared\":{\"type\":\"integer\",\"required\":true,\"minimum\":-2147483648,\"maximum\":2147483647}," +
				"\"onlyA\":{\"type\":\"string\"}," +
				"\"described\":{\"type\":\"integer\",\"minimum\":-2147483648,\"maximum\":2147483647,\"description\":\"From B\"}," +
				"\"onlyB\":{\"type\":\"string\",\"minLength\":1}}");
		}

		[Fact]
		public void conflicting_declarations_fail_naming_the_property_and_both_processes()
		{
			var result = DescriptorSchemaGenerator.Generate(new[] { ("Acme.A", typeof(ConflictAConfig)), ("Acme.B", typeof(ConflictBConfig)) });

			result.Success.Should().BeFalse();
			result.Errors.Should().ContainSingle().Which.Should().StartWith("value: declared incompatibly by Acme.A").And.Contain("Acme.B");
		}

		[Fact]
		public void invalid_processor_types_are_warnings_and_never_in_the_enum()
		{
			var cache = new ProcessCache(new[] { typeof(AbstractListedProcessor), typeof(ProcessesingleItem) });
			var result = DescriptorSchemaGenerator.Generate(cache.Processes);

			result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Errors));
			result.Warnings.Should().ContainSingle().Which.Should().StartWith(typeof(AbstractListedProcessor).FullName);
			result.Schema["process"]["enum"].Values<string>().Should().Equal(typeof(ProcessesingleItem).FullName);
		}

		[Fact]
		public void list_writes_nothing_to_stdout_and_errors_to_stderr_when_the_package_cannot_be_described()
		{
			var action = new ListAction(verbose: false, legacyJson: false, json: false,
				new ProcessCache(new[] { typeof(AbstractListedProcessor) }), applicationLifetime: null);
			var stdout = new StringWriter();
			var stderr = new StringWriter();

			var exitCode = action.Execute(stdout, stderr);

			exitCode.Should().Be(1);
			stdout.ToString().Should().BeEmpty();
			var lines = stderr.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			lines.Should().Contain("ERROR: package: contains no runnable process");
			lines.Should().Contain(l => l.StartsWith("WARN: " + typeof(AbstractListedProcessor).FullName));
		}

		[Fact]
		public void list_writes_exactly_one_json_object_and_a_newline()
		{
			var action = new ListAction(false, false, false, new ProcessCache(new[] { typeof(ProcessesingleItem) }), null);
			var stdout = new StringWriter();
			var stderr = new StringWriter();

			action.Execute(stdout, stderr).Should().Be(0);

			var text = stdout.ToString();
			text.Should().EndWith("\n").And.NotStartWith("\n");
			text.TrimEnd('\n').Should().NotContain("\n");
			DescriptorDialect.Validate(text).Should().BeEmpty();
			stderr.ToString().Should().BeEmpty();
		}

		[Fact]
		public void list_json_is_withdrawn()
		{
			var action = new ListAction(false, false, json: true, new ProcessCache(new[] { typeof(ProcessesingleItem) }), null);
			var stdout = new StringWriter();
			var stderr = new StringWriter();

			action.Execute(stdout, stderr).Should().Be(1);
			stdout.ToString().Should().BeEmpty();
			stderr.ToString().Should().StartWith("ERROR: list --json was withdrawn");
		}
	}
}
