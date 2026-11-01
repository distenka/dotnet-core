using FluentAssertions;
using Distenka.Diagnostics;
using Distenka.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Configuration
{
	public class ValidatedChild
	{
		[Range(1, 5)] public int Level { get; set; }
	}

	public class ValidatedConfig : Config
	{
		[Required] public int Count { get; set; }
		[Required] public string Name { get; set; }
		[RegularExpression("^[a-z]+$")] public string Code { get; set; }
		public ValidatedChild Child { get; set; }
	}

	/// <summary>Fixture for DCS-044 run-time validation (plain members only, safe for the per-processor command builder).</summary>
	public class ValidatedProcessor : Processor<ValidatedConfig>
	{
		public ValidatedProcessor(ValidatedConfig config) : base(config) { }
		public override Task<Result> ProcessAsync() => Task.FromResult(Result.Success());
	}

	/// <summary>DCS-044 / ADR-002 §2.2 rule 3: the resolution order and its two error kinds.</summary>
	public class Process_resolution
	{
		static readonly List<(string FullName, string Name)> Set = new()
		{
			("Acme.Jobs.Import", "Import"),
			("Acme.Legacy.Import", "Import"),
			("Acme.Jobs.Export", "Export"),
			("Acme.Jobs.export", "export"),
			("Acme.Jobs.Sync", "Sync"),
		};

		[Theory]
		[InlineData("Acme.Jobs.Import", 0)]       // step 1: exact canonical name wins over the ambiguous short name
		[InlineData("Acme.Jobs.export", 3)]       // step 1: exact, case-sensitive
		[InlineData("Export", 2)]                 // step 2: exact short name
		[InlineData("sync", 4)]                   // step 3: case-insensitive short name
		[InlineData("ACME.JOBS.SYNC", 4)]         // step 3: case-insensitive canonical name
		public void resolves_in_order(string requested, int expected) =>
			ProcessResolver.Resolve(requested, Set).Should().Be(expected);

		[Fact]
		public void an_ambiguous_short_name_is_an_ambiguity_error_naming_the_candidates()
		{
			var act = () => ProcessResolver.Resolve("Import", Set);

			act.Should().Throw<AmbiguousProcessException>()
				.WithMessage("process 'Import' is ambiguous; it matches Acme.Jobs.Import, Acme.Legacy.Import");
		}

		[Fact]
		public void a_case_insensitive_tie_is_an_ambiguity_error()
		{
			var act = () => ProcessResolver.Resolve("EXPORT", Set);

			act.Should().Throw<AmbiguousProcessException>().WithMessage("*Acme.Jobs.Export, Acme.Jobs.export");
		}

		[Fact]
		public void an_unknown_name_is_distinct_from_ambiguity_and_lists_the_canonical_names()
		{
			var act = () => ProcessResolver.Resolve("Nope", Set);

			act.Should().Throw<TypeNotFoundException>()
				.Where(e => e.GetType() == typeof(TypeNotFoundException))
				.WithMessage("process 'Nope' is not in this package; available: Acme.Jobs.Import, Acme.Legacy.Import, Acme.Jobs.Export, Acme.Jobs.export, Acme.Jobs.Sync");
		}
	}

	/// <summary>DCS-044 / ADR-002 §2.4, ADR-003 §2.4: reading the platform's configuration document.</summary>
	public class Reading_a_configuration
	{
		static ConfigReader Reader(params Type[] processors) => new ConfigReader(new ProcessCache(processors));

		[Fact]
		public void a_document_without_process_runs_the_only_process()
		{
			var config = Reader(typeof(DiagnosticProcessor)).FromJson("{ \"numberOfItems\": 3 }");

			config.Should().BeOfType<DiagnosticConfig>().Which.NumberOfItems.Should().Be(3);
			config.Process.Type.Should().Be(typeof(DiagnosticProcessor).FullName);
		}

		[Fact]
		public void a_document_without_process_fails_when_the_package_has_several()
		{
			var act = () => Reader(typeof(Process0), typeof(ProcessesingleItem)).FromJson("{}");

			act.Should().Throw<ConfigException>().WithMessage(
				"configuration does not name a process and the package contains 2 processes (Distenka.Tests.Process0, Distenka.Tests.ProcessesingleItem)");
		}

		[Theory]
		[InlineData("\"Distenka.Tests.Process0\"")]
		[InlineData("\"Process0\"")]
		[InlineData("\"process0\"")]
		[InlineData("{ \"type\": \"Distenka.Tests.Process0\", \"package\": \"x\", \"version\": \"1.0\" }")]
		[InlineData("{ \"Type\": \"Process0\" }")]
		public void the_process_is_resolved_from_its_canonical_short_case_insensitive_or_legacy_form(string process)
		{
			var config = Reader(typeof(Process0), typeof(ProcessesingleItem)).FromJson($"{{ \"process\": {process} }}");

			config.Process.Type.Should().Be(typeof(Process0).FullName);
		}

		[Fact]
		public void an_unknown_process_lists_the_available_ones()
		{
			var act = () => Reader(typeof(Process0), typeof(ProcessesingleItem)).FromJson("{ \"process\": \"Nope\" }");

			act.Should().Throw<ConfigException>()
				.WithMessage("process 'Nope' is not in this package; available: Distenka.Tests.Process0, Distenka.Tests.ProcessesingleItem");
		}

		[Theory]
		[InlineData("{ \"process\": 5 }", "process: must be a string naming the process")]
		[InlineData("{ \"process\": { \"package\": \"x\" } }", "process.type: must be a string naming the process")]
		[InlineData("[1, 2]", "configuration must be a JSON object")]
		[InlineData("{ \"process\": ", "configuration is not valid JSON*")]
		public void malformed_documents_are_configuration_errors(string json, string message)
		{
			var act = () => Reader(typeof(Process0)).FromJson(json);

			act.Should().Throw<ConfigException>().WithMessage(message);
		}

		[Fact]
		public void a_value_of_the_wrong_type_is_a_configuration_error_naming_the_member()
		{
			var act = () => Reader(typeof(DiagnosticProcessor)).FromJson("{ \"numberOfItems\": \"many\" }");

			act.Should().Throw<ConfigException>().WithMessage("*numberOfItems*");
		}

		const string Validated = "\"process\": \"Distenka.Tests.Scenarios.Configuration.ValidatedProcessor\"";

		[Fact]
		public void members_required_by_the_resolved_process_must_be_present()
		{
			var act = () => Reader(typeof(ValidatedProcessor)).FromJson($"{{ {Validated} }}");

			act.Should().Throw<ConfigException>().WithMessage(
				"configuration for Distenka.Tests.Scenarios.Configuration.ValidatedProcessor is invalid: " +
				"count: required by Distenka.Tests.Scenarios.Configuration.ValidatedProcessor but missing; " +
				"name: required by Distenka.Tests.Scenarios.Configuration.ValidatedProcessor but missing");
		}

		[Fact]
		public void dataannotations_without_a_descriptor_keyword_are_enforced_at_run()
		{
			var act = () => Reader(typeof(ValidatedProcessor)).FromJson(
				$"{{ {Validated}, \"count\": 1, \"name\": \"n\", \"code\": \"ABC\", \"child\": {{ \"level\": 9 }} }}");

			act.Should().Throw<ConfigException>()
				.WithMessage("*code: The field Code must match the regular expression '^[a-z]+$'.*")
				.WithMessage("*child.level: The field Level must be between 1 and 5.*");
		}

		[Fact]
		public void a_valid_document_reads_and_properties_of_other_processes_are_ignored()
		{
			var config = Reader(typeof(ValidatedProcessor)).FromJson(
				$"{{ {Validated}, \"count\": 2, \"name\": \"n\", \"code\": \"abc\", \"child\": {{ \"level\": 3 }}, \"otherProcessSetting\": true }}");

			config.Should().BeOfType<ValidatedConfig>().Which.Child.Level.Should().Be(3);
		}
	}
}
