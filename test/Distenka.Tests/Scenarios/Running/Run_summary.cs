using Distenka.Diagnostics;
using Distenka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	/// <summary>
	/// DCS-046 rules of the run summary line (<see cref="RunSummary.Compose"/>): schema, success-path hygiene (ADR-004
	/// §2.3 rule 2), key redaction, and the 8192-character line cap (ADR-009 §2.2) with the line staying valid JSON.
	/// </summary>
	public class Run_summary_rules
	{
		/// <summary>
		/// The golden summary. The same line is parsed by <c>Distenka.Client.Tests/Run_output_summary.cs</c> — change both together.
		/// </summary>
		internal const string Golden =
			"{\"distenkaSummary\":1,\"disposition\":\"Failed\",\"process\":\"Distenka.Diagnostics.DiagnosticProcessor\"," +
			"\"totalItems\":3,\"successfulItems\":2,\"failedItems\":1," +
			"\"categories\":[{\"category\":\"Rejected\",\"count\":1,\"successful\":false},{\"category\":\"Successful\",\"count\":2,\"successful\":true}]," +
			"\"categoriesNotListed\":0," +
			"\"methods\":{\"initializeAsync\":\"Successful\",\"getItemsAsync\":\"Successful\",\"finalizeAsync\":\"Successful\"}," +
			"\"failures\":[{\"id\":\"2\",\"category\":\"Rejected\",\"message\":\"Customer 2 has no plan\"}],\"failuresNotListed\":0," +
			"\"output\":{\"processed\":3},\"outputTruncated\":false,\"outputOmitted\":false,\"omitted\":0," +
			"\"startedAt\":\"2026-11-01T10:00:00.000Z\",\"completedAt\":\"2026-11-01T10:00:05.250Z\"}";

		const string Hex = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
		const string Key = "DK-" + Hex;

		static RunSummaryFields GoldenFields()
		{
			var fields = new RunSummaryFields
			{
				Disposition = Disposition.Failed,
				Process = "Distenka.Diagnostics.DiagnosticProcessor",
				TotalItems = 3,
				SuccessfulItems = 2,
				FailedItems = 1,
				Output = new Dictionary<string, object> { ["processed"] = 3 },
				StartedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
				CompletedAt = new DateTime(2026, 11, 1, 10, 0, 5, 250, DateTimeKind.Utc),
			};
			fields.Categories.Add(new RunSummaryCategory("Successful", 2, true));
			fields.Categories.Add(new RunSummaryCategory("Rejected", 1, false));
			fields.Methods.Add(new KeyValuePair<ProcessorMethod, bool>(ProcessorMethod.InitializeAsync, true));
			fields.Methods.Add(new KeyValuePair<ProcessorMethod, bool>(ProcessorMethod.GetItemsAsync, true));
			fields.Methods.Add(new KeyValuePair<ProcessorMethod, bool>(ProcessorMethod.FinalizeAsync, true));
			fields.Failures.Add(new RunSummaryFailure("2", "Rejected", "Customer 2 has no plan"));
			return fields;
		}

		static JObject Parse(string line)
		{
			var summary = ProcessRun.ParseSummary(line);
			summary.Should().NotBeNull(line);
			return summary;
		}

		[Fact]
		public void the_summary_matches_the_documented_schema()
		{
			// AC 2 (golden). Failed categories first; failure messages present because the run failed.
			RunSummary.Compose(GoldenFields()).Should().Be(Golden);
		}

		[Fact]
		public void a_run_that_ended_before_its_execution_has_no_counts()
		{
			// ADR-004 §2.2 rows 5 and 7 (configuration error: the process is unknown).
			RunSummary.ForIncompleteRun(null, Disposition.Failed).Should().Be(
				"{\"distenkaSummary\":1,\"disposition\":\"Failed\",\"process\":null,\"totalItems\":null,\"successfulItems\":0,\"failedItems\":0," +
				"\"categories\":[],\"categoriesNotListed\":0,\"methods\":{},\"failures\":[],\"failuresNotListed\":0,\"output\":null," +
				"\"outputTruncated\":false,\"outputOmitted\":false,\"omitted\":0,\"startedAt\":null,\"completedAt\":null}");
		}

		[Theory]
		[InlineData(Disposition.Successful, "Successful")]
		[InlineData(Disposition.Failed, "Failed")]
		[InlineData(Disposition.Cancelled, "Cancelled")]
		[InlineData(Disposition.NotComplete, "Failed")]
		public void the_disposition_is_one_of_three_words(Disposition disposition, string expected)
		{
			Parse(RunSummary.ForIncompleteRun("P", disposition)).Value<string>("disposition").Should().Be(expected);
		}

		[Fact]
		public void on_the_success_path_user_text_that_would_fail_the_run_is_omitted()
		{
			// AC 4 / AC 5: no Exception / error: / ERROR: on the success path; omitted texts are flagged and counted.
			var fields = new RunSummaryFields
			{
				Disposition = Disposition.Successful,
				Process = "Ns.Sync",
				TotalItems = 3,
				SuccessfulItems = 2,
				FailedItems = 1,
				Output = new { note = "retried after error: timeout" },
			};
			fields.Categories.Add(new RunSummaryCategory("Synced", 2, true));
			fields.Categories.Add(new RunSummaryCategory("TimeoutException", 1, false));
			fields.Failures.Add(new RunSummaryFailure("item-7", "TimeoutException", "The operation has timed out."));

			var line = RunSummary.Compose(fields);

			RunOutcome.ContainsErrorTrigger(line).Should().BeFalse(line);
			ExecutorRules.IsErrorStdoutLine(line).Should().BeFalse(line);
			var summary = Parse(line);
			summary.Value<string>("disposition").Should().Be("Successful");
			summary["categories"].Select(c => c.Value<string>("category")).Should().Equal(new[] { ReporterText.Omitted, "Synced" }, line);
			var failure = summary["failures"].Should().ContainSingle().Which;
			failure.Value<string>("id").Should().Be("item-7");
			failure.Value<string>("category").Should().Be(ReporterText.Omitted);
			failure["message"].Should().BeNull("messages are only written when the run did not succeed");
			summary["output"].Type.Should().Be(JTokenType.Null);
			summary.Value<bool>("outputOmitted").Should().BeTrue();
			summary.Value<int>("omitted").Should().Be(3, "the failed category, the failure's category and the output");
		}

		[Fact]
		public void on_a_failed_run_the_same_texts_are_kept()
		{
			var fields = new RunSummaryFields { Disposition = Disposition.Failed, FailedItems = 1, Output = "error: partial" };
			fields.Categories.Add(new RunSummaryCategory("TimeoutException", 1, false));
			fields.Failures.Add(new RunSummaryFailure("item-7", "TimeoutException", "The operation has timed out."));

			var summary = Parse(RunSummary.Compose(fields));

			summary["categories"][0].Value<string>("category").Should().Be("TimeoutException");
			summary["failures"][0].Value<string>("message").Should().Be("The operation has timed out.");
			summary.Value<string>("output").Should().Be("error: partial");
			summary.Value<int>("omitted").Should().Be(0);
		}

		[Fact]
		public void key_shaped_values_are_redacted_everywhere_in_the_line()
		{
			// AC 1: marker + at least 32 hex digits → the 8-digit public prefix and "…".
			var fields = new RunSummaryFields
			{
				Disposition = Disposition.Failed,
				Process = "Ns.P",
				FailedItems = 1,
				Output = new { key = Key.ToLowerInvariant(), invocationKey = "DKI-" + Hex },
			};
			fields.Categories.Add(new RunSummaryCategory(Key, 1, false));
			fields.Failures.Add(new RunSummaryFailure(Key, Key, $"rejected {Key}"));

			var line = RunSummary.Compose(fields);

			line.Should().NotContainEquivalentOf(Hex.Substring(8));
			var summary = Parse(line);
			summary["categories"][0].Value<string>("category").Should().Be("DK-01234567…");
			summary["failures"][0].Value<string>("message").Should().Be("rejected DK-01234567…");
			summary["output"].Value<string>("key").Should().Be("dk-01234567…");
			summary["output"].Value<string>("invocationKey").Should().Be("DKI-01234567…");
		}

		[Fact]
		public void a_public_key_prefix_is_not_redacted()
		{
			var fields = new RunSummaryFields { Disposition = Disposition.Successful, Output = "issued by DK-0123ABCD" };

			Parse(RunSummary.Compose(fields)).Value<string>("output").Should().Be("issued by DK-0123ABCD");
		}

		[Fact]
		public void a_long_text_output_is_cut_to_fit_the_line()
		{
			// AC 3, with the ADR-009 §2.2 line cap (8192 characters) as the bound.
			var fields = new RunSummaryFields { Disposition = Disposition.Successful, Output = new string('x', 20000) };

			var line = RunSummary.Compose(fields);

			line.Length.Should().BeLessOrEqualTo(RunOutcome.MaxLineLength);
			var summary = Parse(line);
			summary.Value<bool>("outputTruncated").Should().BeTrue();
			summary.Value<string>("output").Should().StartWith("xxxx").And.EndWith("…");
			summary.Value<string>("output").Length.Should().BeGreaterThan(7000, "only what does not fit is cut");
		}

		[Fact]
		public void a_long_structured_output_is_dropped()
		{
			var fields = new RunSummaryFields { Disposition = Disposition.Successful, Output = Enumerable.Range(0, 5000).ToArray() };

			var line = RunSummary.Compose(fields);

			line.Length.Should().BeLessOrEqualTo(RunOutcome.MaxLineLength);
			var summary = Parse(line);
			summary["output"].Type.Should().Be(JTokenType.Null);
			summary.Value<bool>("outputTruncated").Should().BeTrue();
		}

		[Fact]
		public void many_long_texts_still_fit_and_stay_counted()
		{
			var fields = new RunSummaryFields
			{
				Disposition = Disposition.Failed,
				Process = new string('p', 1000),
				FailedItems = 40,
				Output = new string('o', 9000),
			};
			for (var i = 0; i < 30; i++)
			{
				fields.Categories.Add(new RunSummaryCategory($"category-{i:D2}-" + new string('c', 600), 1, false));
				fields.Failures.Add(new RunSummaryFailure($"item-{i:D2}-" + new string('i', 600), "c" + new string('c', 600), "m" + new string('\n', 600)));
			}

			var line = RunSummary.Compose(fields);

			line.Length.Should().BeLessOrEqualTo(RunOutcome.MaxLineLength);
			line.Should().NotContain("\n", "one physical line");
			var summary = Parse(line);
			var failures = summary["failures"].Count();
			failures.Should().BeInRange(0, RunSummary.MaxListed);
			(failures + summary.Value<int>("failuresNotListed")).Should().Be(40);
			var categories = summary["categories"].Count();
			(categories + summary.Value<int>("categoriesNotListed")).Should().Be(30);
			summary["failures"].Concat(summary["categories"])
				.SelectMany(t => ((JObject)t).Properties().Select(p => p.Value))
				.Where(v => v.Type == JTokenType.String)
				.Should().OnlyContain(v => ((string)v).Length <= RunSummary.MaxTextLength + 1, "user texts are bounded");
		}

		[Fact]
		public void an_output_that_cannot_be_serialized_is_omitted_without_failing()
		{
			var loop = new Loop();
			loop.Self = loop;

			var summary = Parse(RunSummary.Compose(new RunSummaryFields { Disposition = Disposition.Successful, Output = loop }));

			summary["output"].Type.Should().Be(JTokenType.Null);
			summary.Value<bool>("outputOmitted").Should().BeTrue();
		}

		sealed class Loop
		{
			public Loop Self { get; set; }
		}
	}

	/// <summary>
	/// DCS-046, process-level: the run summary is the last stdout line for every ADR-004 §2.2 outcome the SDK controls,
	/// after the <c>ERROR:</c> line when there is one. Spawns the built Distenka.Diagnostics / Distenka.Tests.Cli.
	/// </summary>
	public class Run_summary
	{
		const string ProcessKey = "\"process\": \"Distenka.Diagnostics.DiagnosticProcessor\"";

		static JObject SummaryOf(ProcessRun run)
		{
			var summary = run.Summary;
			summary.Should().NotBeNull("the last stdout line is the run summary; " + run);
			run.Stdout.Count(l => l.StartsWith("{\"distenkaSummary\":", StringComparison.Ordinal)).Should().Be(1, run.ToString());
			return summary;
		}

		static void ErrorLineComesRightBeforeTheSummary(ProcessRun run)
		{
			run.ErrorLines.Should().ContainSingle(run.ToString());
			run.Stdout[run.Stdout.Count - 2].Should().StartWith("ERROR: ", run.ToString());
		}

		[Fact]
		public async Task a_successful_run_ends_with_a_Successful_summary()
		{
			// ADR-004 §2.2 row 1.
			var run = await ProcessorProcess.RunDiagnosticsAsync($"{{ {ProcessKey}, \"numberOfItems\": 5, \"millisecondDelayPerItem\": 0 }}");

			run.ExitCode.Should().Be(0, run.ToString());
			var summary = SummaryOf(run);
			summary.Value<int>("distenkaSummary").Should().Be(1);
			summary.Value<string>("disposition").Should().Be("Successful");
			summary.Value<string>("process").Should().Be("Distenka.Diagnostics.DiagnosticProcessor");
			summary.Value<int?>("totalItems").Should().Be(5);
			summary.Value<int>("successfulItems").Should().Be(5);
			summary.Value<int>("failedItems").Should().Be(0);
			summary["failures"].Should().BeEmpty();
			summary["methods"].Value<string>("initializeAsync").Should().Be("Successful");
			summary["methods"].Value<string>("finalizeAsync").Should().Be("Successful");
			DateTimeOffset.Parse(summary.Value<string>("startedAt")).Should().BeOnOrBefore(DateTimeOffset.Parse(summary.Value<string>("completedAt")));
			ExecutorRules.FinalState(run).Should().Be("Completed", "the summary must not trip the executor; " + run);
		}

		[Fact]
		public async Task tolerated_item_exceptions_leave_no_exception_text_in_the_summary()
		{
			// ADR-004 §2.2 row 2: 3 items throw, below the threshold of 10 → Successful; category = exception type name.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInProcessAsync\": true, " +
				"\"execution\": { \"itemFailureCountToStopProcess\": 10 } }");

			run.ExitCode.Should().Be(0, run.ToString());
			var summary = SummaryOf(run);
			summary.Value<string>("disposition").Should().Be("Successful");
			summary.Value<int>("failedItems").Should().Be(3);
			run.Stdout.Last().Should().NotContain("Exception").And.NotContainEquivalentOf("error:");
			summary["failures"].Select(f => f.Value<string>("id")).Should().BeEquivalentTo(new[] { "0", "1", "2" });
			summary["failures"].Should().OnlyContain(f => f["message"] == null);
			summary.Value<int>("omitted").Should().BeGreaterThan(0);
			ExecutorRules.FinalState(run).Should().Be("Completed", run.ToString());
		}

		[Fact]
		public async Task an_item_failure_threshold_ends_with_the_ERROR_line_then_a_Failed_summary()
		{
			// ADR-004 §2.2 row 3.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInProcessAsync\": true }}");

			run.ExitCode.Should().Be(1, run.ToString());
			ErrorLineComesRightBeforeTheSummary(run);
			var summary = SummaryOf(run);
			summary.Value<string>("disposition").Should().Be("Failed");
			summary.Value<int>("failedItems").Should().BeGreaterThan(0);
			summary["failures"].Should().NotBeEmpty().And.OnlyContain(f => f.Value<string>("message") != null, "a failed run carries the messages");
		}

		[Fact]
		public async Task a_failed_InitializeAsync_is_named_in_the_methods()
		{
			// ADR-004 §2.2 row 4.
			var run = await ProcessorProcess.RunDiagnosticsAsync(
				$"{{ {ProcessKey}, \"numberOfItems\": 3, \"millisecondDelayPerItem\": 0, \"throwExceptionInInitializeAsync\": true }}");

			run.ExitCode.Should().Be(1, run.ToString());
			ErrorLineComesRightBeforeTheSummary(run);
			var summary = SummaryOf(run);
			summary.Value<string>("disposition").Should().Be("Failed");
			summary["methods"].Value<string>("initializeAsync").Should().Be("Failed");
		}

		[Fact]
		public async Task a_configuration_error_ends_with_the_ERROR_line_then_a_Failed_summary_without_counts()
		{
			// ADR-004 §2.2 row 7.
			var run = await ProcessorProcess.RunConfigAsync(ProcessorProcess.TestsCliDll, "{ \"process\": \"Nope\" }");

			run.ExitCode.Should().Be(1, run.ToString());
			ErrorLineComesRightBeforeTheSummary(run);
			var summary = SummaryOf(run);
			summary.Value<string>("disposition").Should().Be("Failed");
			summary["process"].Type.Should().Be(JTokenType.Null);
			summary["totalItems"].Type.Should().Be(JTokenType.Null);
			run.Stderr.Should().BeEmpty(run.ToString());
		}
	}

	/// <summary>
	/// DCS-046, in-process (all OSes): a host shutdown during a run ends with a <c>Cancelled</c> summary (ADR-004 §2.2 row 6).
	/// Shares the collection that redirects <see cref="Console.Out"/>.
	/// </summary>
	[Collection(nameof(InProcessRunCollection))]
	public class Run_summary_cancellation
	{
		[Fact]
		public async Task host_shutdown_during_a_run_ends_with_a_Cancelled_summary()
		{
			var dir = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			var configPath = Path.Combine(dir, "config.json");
			File.WriteAllText(configPath, "{ \"process\": \"Distenka.Diagnostics.DiagnosticProcessor\", \"numberOfItems\": 1000, \"millisecondDelayPerItem\": 20 }");

			var originalOut = Console.Out;
			var output = new StringWriter();

			try
			{
				Console.SetOut(output);

				// --plain makes the mode explicit: whether the test runner's own stdout is redirected does not matter.
				using var host = ProcessorHost.CreateDefaultBuilder(new[] { "run", configPath, "--silent", "--plain" }, typeof(DiagnosticProcessor).Assembly).Build();

				var running = host.RunAsync();
				await Task.Delay(TimeSpan.FromSeconds(1));
				host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
				await running;

				var lines = output.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				lines.Length.Should().BeGreaterThanOrEqualTo(2, output.ToString());
				lines[lines.Length - 2].Should().Be("ERROR: run cancelled (host shutdown requested)");
				var summary = ProcessRun.ParseSummary(lines[lines.Length - 1]);
				summary.Should().NotBeNull(output.ToString());
				summary.Value<string>("disposition").Should().Be("Cancelled");
			}
			finally
			{
				Console.SetOut(originalOut);
				Environment.ExitCode = 0;
			}
		}
	}
}
