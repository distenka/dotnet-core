using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Distenka.Hosting
{
	/// <summary>
	/// The run summary (DCS-046, ADR-004 §2.2/§2.4): one JSON object written as the <b>last</b> stdout line of every
	/// plain-mode <c>run</c> — after the <c>ERROR:</c> line when there is one — so that a consumer reading the run's
	/// <c>output</c> (<c>details.output</c>, the capability result's <c>output</c>) can tell Successful, Failed and Cancelled
	/// apart, which the binary exit code cannot (AD-3).
	/// </summary>
	/// <remarks>
	/// <para>Schema (version 1; every member always present, camelCase):</para>
	/// <code>
	/// { "distenkaSummary": 1, "disposition": "Successful|Failed|Cancelled", "process": "Ns.Type",
	///   "totalItems": 3, "successfulItems": 2, "failedItems": 1,
	///   "categories": [ { "category": "…", "count": 1, "successful": false } ], "categoriesNotListed": 0,
	///   "methods": { "initializeAsync": "Successful", … }, "failures": [ { "id": "…", "category": "…", "message": "…" } ],
	///   "failuresNotListed": 0, "output": &lt;FinalizeAsync output as JSON&gt;, "outputTruncated": false, "outputOmitted": false,
	///   "omitted": 0, "startedAt": "2026-11-01T10:00:00.000Z", "completedAt": "…" }
	/// </code>
	/// <para>
	/// Rules: at most <see cref="MaxListed"/> categories and failures (the rest counted in <c>…NotListed</c>); user texts
	/// cut at <see cref="MaxTextLength"/> characters; failure <c>message</c>s only when the run did not succeed (ADR-004
	/// §2.3 rule 2/3); on the success path a user text containing an executor error trigger is replaced by
	/// <c>(omitted)</c> (the <c>output</c> is dropped, <c>outputOmitted</c>) and counted in <c>omitted</c>; key-shaped
	/// values are redacted (<see cref="SecretText"/>); the line is at most <see cref="RunOutcome.MaxLineLength"/>
	/// characters (ADR-009 §2.2) — a longer <c>output</c> is cut (string) or dropped (structured) with
	/// <c>outputTruncated</c>, then failures and categories are moved to the counts. The line is always valid JSON.
	/// </para>
	/// </remarks>
	internal static class RunSummary
	{
		/// <summary>The schema version, written as <c>distenkaSummary</c> — also the marker that tells this line from a processor's own JSON.</summary>
		public const int Version = 1;

		/// <summary>The most categories and the most failures listed.</summary>
		public const int MaxListed = 10;

		/// <summary>The most characters kept of one user text (category, item id, failure message, process).</summary>
		public const int MaxTextLength = 256;

		static readonly JsonSerializer OutputSerializer = JsonSerializer.Create(new JsonSerializerSettings
		{
			ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
			ReferenceLoopHandling = ReferenceLoopHandling.Error,
			MaxDepth = 64,
		});

		/// <summary>The summary of a finished execution.</summary>
		/// <param name="execution">The execution, complete.</param>
		/// <param name="log">The result log (item failures, <c>FinalizeAsync</c> output); <c>null</c> when none was kept.</param>
		/// <param name="processType">The process that ran.</param>
		/// <param name="disposition">The run's outcome — the execution's, or Failed when the run failed after it (e.g. writing the results file).</param>
		public static string ForExecution(Execution execution, ResultLog log, string processType, Disposition disposition)
		{
			if (execution is null)
				throw new ArgumentNullException(nameof(execution));

			var fields = new RunSummaryFields
			{
				Disposition = disposition,
				Process = processType,
				TotalItems = execution.TotalItemCount,
				SuccessfulItems = execution.SuccessfulItemCount,
				FailedItems = execution.FailedItemCount,
				StartedAt = execution.StartedAt,
				CompletedAt = execution.CompletedAt,
			};

			foreach (var category in execution.ItemCategories)
				fields.Categories.Add(new RunSummaryCategory(category.Category ?? (category.IsSuccessful ? Result.Successful : Result.Failed), category.Count, category.IsSuccessful));

			foreach (var method in execution.Methods.Values.OrderBy(m => m.Method))
				fields.Methods.Add(new KeyValuePair<ProcessorMethod, bool>(method.Method, method.IsSuccessful));

			if (log != null)
			{
				foreach (var item in log.FailedItems.OrderBy(i => i.Id, StringComparer.Ordinal).Take(MaxListed))
					fields.Failures.Add(new RunSummaryFailure(item.Id, item.Category, item.Methods.Values.FirstOrDefault(m => !m.IsSuccessful)?.Error?.Message));

				fields.Output = log.Output;
			}

			return Compose(fields);
		}

		/// <summary>
		/// The summary of a run that ended before its execution completed (ADR-004 §2.2 rows 5 and 7): a configuration or
		/// start-up error, or an exception that escaped the pipeline. Counts are zero and the timestamps <c>null</c>.
		/// </summary>
		public static string ForIncompleteRun(string processType, Disposition disposition) =>
			Compose(new RunSummaryFields { Disposition = disposition, Process = processType });

		/// <summary>Renders <paramref name="fields"/> as the summary line, applying every rule in the remarks.</summary>
		public static string Compose(RunSummaryFields fields)
		{
			if (fields is null)
				throw new ArgumentNullException(nameof(fields));

			var successPath = fields.Disposition == Disposition.Successful;
			var state = new State { Disposition = fields.Disposition };

			state.Process = UserText(fields.Process, successPath, ref state.Omitted);
			state.TotalItems = fields.TotalItems;
			state.SuccessfulItems = fields.SuccessfulItems;
			state.FailedItems = fields.FailedItems;
			state.StartedAt = fields.StartedAt;
			state.CompletedAt = fields.CompletedAt;

			var categories = fields.Categories
				.OrderBy(c => c.Successful)
				.ThenBy(c => c.Category, StringComparer.Ordinal)
				.ToList();

			foreach (var c in categories.Take(MaxListed))
				state.Categories.Add(new RunSummaryCategory(UserText(c.Category, successPath, ref state.Omitted), c.Count, c.Successful));

			state.CategoriesNotListed = categories.Count - state.Categories.Count;

			foreach (var method in fields.Methods)
				state.Methods.Add(method);

			foreach (var f in fields.Failures.Take(MaxListed))
				state.Failures.Add(new RunSummaryFailure(
					UserText(f.Id ?? "unknown", successPath, ref state.Omitted),
					UserText(f.Category, successPath, ref state.Omitted),
					successPath ? null : Bound(f.Message)));

			state.FailuresNotListed = Math.Max(0, fields.FailedItems - state.Failures.Count);

			if (fields.Output != null)
			{
				try
				{
					var output = JToken.FromObject(fields.Output, OutputSerializer);

					if (successPath && RunOutcome.ContainsErrorTrigger(output.ToString(Formatting.None)))
					{
						state.OutputOmitted = true;
						state.Omitted++;
					}
					else
					{
						state.Output = output;
					}
				}
				catch (Exception)
				{
					// Not serializable (a reference loop, a type that throws): the summary is still written, without it.
					// The reason is not printed — on the success path it would be an exception text on stdout.
					state.OutputOmitted = true;
				}
			}

			var line = Render(state);

			while (line.Length > RunOutcome.MaxLineLength && Shrink(state, line.Length - RunOutcome.MaxLineLength))
				line = Render(state);

			if (successPath && RunOutcome.ContainsErrorTrigger(line))
			{
				// Defence in depth (ADR-004 §2.3 rule 2): should anything still trip the executor, drop every user text.
				state.Omitted++;
				state.Process = null;
				state.CategoriesNotListed += state.Categories.Count;
				state.Categories.Clear();
				state.Failures.Clear();
				state.FailuresNotListed = state.FailedItems;
				state.OutputOmitted |= state.Output != null;
				state.Output = null;
				line = Render(state);
			}

			return line;
		}

		/// <summary>Makes the summary smaller by one step; false when nothing is left to shrink.</summary>
		static bool Shrink(State state, int excess)
		{
			if (state.Output != null)
			{
				state.OutputTruncated = true;

				if (state.Output.Type == JTokenType.String)
				{
					var text = (string)state.Output;

					// Each character costs at least one character of JSON, so cutting the excess (plus room for "…") converges.
					var keep = text.Length - excess - 8;
					state.Output = keep > 0 ? new JValue(Cut(text, keep) + "…") : null;
				}
				else
				{
					state.Output = null;
				}

				return true;
			}

			if (state.Failures.Count > 0)
			{
				state.Failures.RemoveAt(state.Failures.Count - 1);
				state.FailuresNotListed++;
				return true;
			}

			if (state.Categories.Count > 0)
			{
				state.Categories.RemoveAt(state.Categories.Count - 1);
				state.CategoriesNotListed++;
				return true;
			}

			if (state.Process != null)
			{
				state.Process = null;
				return true;
			}

			return false;
		}

		static string Render(State state)
		{
			var summary = new JObject
			{
				["distenkaSummary"] = Version,
				["disposition"] = DispositionName(state.Disposition),
				["process"] = state.Process,
				["totalItems"] = state.TotalItems,
				["successfulItems"] = state.SuccessfulItems,
				["failedItems"] = state.FailedItems,
				["categories"] = new JArray(state.Categories.Select(c => new JObject
				{
					["category"] = c.Category,
					["count"] = c.Count,
					["successful"] = c.Successful,
				})),
				["categoriesNotListed"] = state.CategoriesNotListed,
				["methods"] = new JObject(state.Methods.Select(m => new JProperty(CamelCase(m.Key.ToString()), m.Value ? Result.Successful : Result.Failed))),
				["failures"] = new JArray(state.Failures.Select(f =>
				{
					var failure = new JObject { ["id"] = f.Id, ["category"] = f.Category };
					if (f.Message != null)
						failure["message"] = f.Message;
					return failure;
				})),
				["failuresNotListed"] = state.FailuresNotListed,
				["output"] = state.Output?.DeepClone() ?? JValue.CreateNull(),
				["outputTruncated"] = state.OutputTruncated,
				["outputOmitted"] = state.OutputOmitted,
				["omitted"] = state.Omitted,
				["startedAt"] = Timestamp(state.StartedAt),
				["completedAt"] = Timestamp(state.CompletedAt),
			};

			return SecretText.Redact(summary.ToString(Formatting.None));
		}

		static string DispositionName(Disposition disposition) =>
			disposition == Disposition.Successful || disposition == Disposition.Cancelled ? disposition.ToString() : Disposition.Failed.ToString();

		static string UserText(string text, bool successPath, ref int omitted)
		{
			if (text is null)
				return null;

			if (successPath && RunOutcome.ContainsErrorTrigger(text))
			{
				omitted++;
				return ReporterText.Omitted;
			}

			return Bound(text);
		}

		static string Bound(string text) =>
			text is null || text.Length <= MaxTextLength ? text : Cut(text, MaxTextLength) + "…";

		/// <summary>The first <paramref name="length"/> characters, never splitting a surrogate pair.</summary>
		static string Cut(string text, int length)
		{
			if (length >= text.Length)
				return text;

			if (length > 0 && char.IsHighSurrogate(text[length - 1]))
				length--;

			return text.Substring(0, length);
		}

		static string CamelCase(string name) =>
			string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

		static string Timestamp(DateTime? value)
		{
			if (value is not DateTime time)
				return null;

			var utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
			return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
		}

		/// <summary>The summary as it is rendered: user texts already checked and bounded.</summary>
		sealed class State
		{
			public Disposition Disposition;
			public string Process;
			public int? TotalItems;
			public int SuccessfulItems;
			public int FailedItems;
			public readonly List<RunSummaryCategory> Categories = new List<RunSummaryCategory>();
			public int CategoriesNotListed;
			public readonly List<KeyValuePair<ProcessorMethod, bool>> Methods = new List<KeyValuePair<ProcessorMethod, bool>>();
			public readonly List<RunSummaryFailure> Failures = new List<RunSummaryFailure>();
			public int FailuresNotListed;
			public JToken Output;
			public bool OutputTruncated;
			public bool OutputOmitted;
			public int Omitted;
			public DateTime? StartedAt;
			public DateTime? CompletedAt;
		}
	}

	/// <summary>What a run summary is built from (raw: <see cref="RunSummary.Compose"/> applies the rules).</summary>
	internal sealed class RunSummaryFields
	{
		public Disposition Disposition { get; set; } = Disposition.Failed;
		public string Process { get; set; }
		public int? TotalItems { get; set; }
		public int SuccessfulItems { get; set; }
		public int FailedItems { get; set; }
		public List<RunSummaryCategory> Categories { get; } = new List<RunSummaryCategory>();
		public List<KeyValuePair<ProcessorMethod, bool>> Methods { get; } = new List<KeyValuePair<ProcessorMethod, bool>>();

		/// <summary>The failed items to list, in order; <see cref="RunSummary.Compose"/> lists the first <see cref="RunSummary.MaxListed"/>.</summary>
		public List<RunSummaryFailure> Failures { get; } = new List<RunSummaryFailure>();

		/// <summary>The <c>FinalizeAsync</c> output; <c>null</c> for none.</summary>
		public object Output { get; set; }

		public DateTime? StartedAt { get; set; }
		public DateTime? CompletedAt { get; set; }
	}

	/// <summary>One item category of a run summary.</summary>
	internal sealed class RunSummaryCategory
	{
		public RunSummaryCategory(string category, int count, bool successful)
		{
			Category = category;
			Count = count;
			Successful = successful;
		}

		public string Category { get; }
		public int Count { get; }
		public bool Successful { get; }
	}

	/// <summary>One failed item of a run summary.</summary>
	internal sealed class RunSummaryFailure
	{
		public RunSummaryFailure(string id, string category, string message)
		{
			Id = id;
			Category = category;
			Message = message;
		}

		public string Id { get; }
		public string Category { get; }
		public string Message { get; }
	}
}
