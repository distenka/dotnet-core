using FluentAssertions;
using Distenka.Diagnostics;
using Distenka.Hosting;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Xunit;

namespace Distenka.Tests.Scenarios.Configuration
{
	/// <summary>
	/// DCS-045 / AD-4: the configuration carries no hosted-platform fields (organization, environment, API key, run id,
	/// queueing hints, API URL). Golden test of the serialized default config, plus backward compatibility for 1.x files.
	/// </summary>
	public class Default_config_serialization
	{
		static readonly string[] ExecutionMembers =
		{
			"parallelTaskCount", "itemFailureRetryCount", "itemFailureCountToStopProcess", "handleExceptions",
			"resultsToFile", "resultsFilePath", "resultsToConsole", "launchDebugger"
		};

		static readonly string[] RemovedMembers =
		{
			"distenkaApi", "orgName", "apiKey", "env", "processor", "runId", "delay", "runAfterId"
		};

		[Fact]
		public void default_config_serializes_only_process_and_execution()
		{
			var json = JObject.Parse(ConfigWriter.ToJson(new Config()));

			json.Properties().Select(p => p.Name).Should().BeEquivalentTo(new[] { "process", "execution" });
			((JObject)json["execution"]).Properties().Select(p => p.Name).Should().BeEquivalentTo(ExecutionMembers);
		}

		[Fact]
		public void serialized_default_config_contains_no_hosted_platform_field()
		{
			var json = ConfigWriter.ToJson(new DiagnosticConfig());
			var names = JObject.Parse(json).Descendants().OfType<JProperty>().Select(p => p.Name).ToList();

			names.Should().NotContain(RemovedMembers);
			json.Should().NotContain("distenka.ai");
		}

		[Fact]
		public void reduced_config_has_no_execution_section()
		{
			var json = JObject.Parse(ConfigWriter.ToReducedJson(new DiagnosticConfig()));

			json.Properties().Select(p => p.Name).Should().NotContain(new[] { "execution", "distenkaApi" });
		}

		[Fact]
		public void a_1x_config_with_the_removed_fields_still_loads()
		{
			// Newtonsoft ignores unknown members by default, so 1.x files keep working; the removed values are dropped.
			var reader = new ConfigReader(new ProcessCache(new[] { typeof(DiagnosticProcessor).Assembly }));

			var config = reader.FromJson(@"{
				""process"": ""Distenka.Diagnostics.DiagnosticProcessor"",
				""numberOfItems"": 7,
				""execution"": {
					""orgName"": ""acme"", ""apiKey"": ""not-a-secret"", ""env"": ""prod"", ""processor"": ""x"",
					""runId"": ""r1"", ""delay"": 5, ""runAfterId"": ""6f9619ff-8b86-d011-b42d-00cf4fc964ff"",
					""parallelTaskCount"": 3
				},
				""distenkaApi"": { ""uri"": ""https://api.distenka.ai/"", ""organizationId"": ""acme"", ""logSuccessfulItemResults"": true }
			}");

			config.Should().BeOfType<DiagnosticConfig>().Which.NumberOfItems.Should().Be(7);
			config.Execution.ParallelTaskCount.Should().Be(3);
			ConfigWriter.ToJson(config).Should().NotContain("not-a-secret").And.NotContain("distenka.ai");
		}

		[Fact]
		public void no_source_file_under_src_references_the_hosted_api()
		{
			// AC: grep for `distenka.ai` in src/ returns nothing (no compiled-in host, AD-4).
			var root = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (root != null && !File.Exists(Path.Combine(root.FullName, "Distenka.sln")))
				root = root.Parent;

			root.Should().NotBeNull("the tests run from inside the repository");

			var src = Path.Combine(root.FullName, "src");
			var hits = Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
				.Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(d => d is "bin" or "obj" or ".vs"))
				.Where(f => new[] { ".cs", ".csproj", ".json", ".props", ".targets", ".md" }.Contains(Path.GetExtension(f)))
				.Where(f => File.ReadAllText(f).Contains("distenka.ai"))
				.Select(f => Path.GetRelativePath(root.FullName, f))
				.ToList();

			hits.Should().BeEmpty();
		}
	}
}
