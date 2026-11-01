using Distenka.Client.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>How a processor's run ended, as its run summary states it (<see cref="RunOutputSummary.Disposition"/>).</summary>
	public enum RunDisposition
	{
		/// <summary>A disposition this version of the SDK does not know.</summary>
		Unknown = 0,

		/// <summary>The processor completed successfully.</summary>
		Successful,

		/// <summary>The processor failed: an item-failure threshold, an exception in a processor method, or a configuration error.</summary>
		Failed,

		/// <summary>The processor was cancelled.</summary>
		Cancelled,
	}

	/// <summary>
	/// The run summary a processor built with the Distenka SDK writes as the last line of its output (DCS-046, ADR-004
	/// §2.2): the processor's own disposition, item counts and categories, the outcome of each processor method, the first
	/// failed items and the <c>FinalizeAsync</c> output. Read it with <see cref="RunDetails.TryParseSummary"/> or
	/// <see cref="Distenka.Client.Invocations.RunResult.TryParseSummary"/>.
	/// </summary>
	/// <remarks>
	/// The platform's own state says Completed or Failed; only the summary says whether a failed run was cancelled, and
	/// what the processor returned. The summary is missing when the run's output exceeded the platform's 2000-line cap,
	/// when the process was killed, and for packages built with an SDK before 2.0.
	/// </remarks>
	public sealed class RunOutputSummary
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunOutputSummary(int version, string? dispositionName, string? process, int? totalItems, int successfulItems, int failedItems,
			IReadOnlyList<RunOutputCategory>? categories, int categoriesNotListed, IReadOnlyDictionary<string, string>? methods,
			IReadOnlyList<RunOutputFailure>? failures, int failuresNotListed, JsonElement? output, bool outputTruncated, bool outputOmitted,
			int omitted, DateTimeOffset? startedAt, DateTimeOffset? completedAt)
		{
			Version = version;
			DispositionName = dispositionName;
			Process = process;
			TotalItems = totalItems;
			SuccessfulItems = successfulItems;
			FailedItems = failedItems;
			Categories = categories ?? Array.Empty<RunOutputCategory>();
			CategoriesNotListed = categoriesNotListed;
			Methods = methods ?? new Dictionary<string, string>();
			Failures = failures ?? Array.Empty<RunOutputFailure>();
			FailuresNotListed = failuresNotListed;
			Output = output is JsonElement value && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined ? value : (JsonElement?)null;
			OutputTruncated = outputTruncated;
			OutputOmitted = outputOmitted;
			Omitted = omitted;
			StartedAt = startedAt;
			CompletedAt = completedAt;
		}

		/// <summary>The summary's schema version (<c>distenkaSummary</c>); <c>1</c> today. Newer versions only add members.</summary>
		[JsonPropertyName("distenkaSummary")]
		public int Version { get; }

		/// <summary><see cref="DispositionName"/> parsed; <see cref="RunDisposition.Unknown"/> for a word this SDK does not know.</summary>
		[JsonIgnore]
		public RunDisposition Disposition => ParseDisposition(DispositionName);

		/// <summary>The <c>disposition</c> exactly as written: <c>Successful</c>, <c>Failed</c> or <c>Cancelled</c>.</summary>
		[JsonPropertyName("disposition")]
		public string? DispositionName { get; }

		/// <summary>The process that ran (full type name); <see langword="null"/> when the configuration could not be read.</summary>
		public string? Process { get; }

		/// <summary>The number of items to process; <see langword="null"/> when the processor could not count them in advance.</summary>
		public int? TotalItems { get; }

		/// <summary>Items that ended successfully.</summary>
		public int SuccessfulItems { get; }

		/// <summary>Items that ended unsuccessfully.</summary>
		public int FailedItems { get; }

		/// <summary>Item categories with their counts, failed ones first; at most 10 (see <see cref="CategoriesNotListed"/>).</summary>
		public IReadOnlyList<RunOutputCategory> Categories { get; }

		/// <summary>How many further categories exist but are not listed.</summary>
		public int CategoriesNotListed { get; }

		/// <summary>
		/// The outcome of each processor method that ran (<c>initializeAsync</c>, <c>getItemsAsync</c>, <c>count</c>,
		/// <c>getEnumerator</c>, <c>finalizeAsync</c>): <c>Successful</c> or <c>Failed</c>.
		/// </summary>
		public IReadOnlyDictionary<string, string> Methods { get; }

		/// <summary>
		/// The first failed items (at most 10, see <see cref="FailuresNotListed"/>). A failure carries a
		/// <see cref="RunOutputFailure.Message"/> only when the run did not succeed.
		/// </summary>
		public IReadOnlyList<RunOutputFailure> Failures { get; }

		/// <summary>How many further failed items exist but are not listed.</summary>
		public int FailuresNotListed { get; }

		/// <summary>
		/// What the processor's <c>FinalizeAsync</c> returned, as JSON; <see langword="null"/> when it returned nothing or
		/// the output was not included (<see cref="OutputTruncated"/>, <see cref="OutputOmitted"/>).
		/// </summary>
		public JsonElement? Output { get; }

		/// <summary>The output was too long for the summary line: a text output was cut (ends with <c>…</c>), a structured one dropped.</summary>
		public bool OutputTruncated { get; }

		/// <summary>The output was left out: on a successful run it contained text the platform treats as an error, or it could not be serialized.</summary>
		public bool OutputOmitted { get; }

		/// <summary>How many user texts (categories, item ids, the output) were replaced by <c>(omitted)</c> on a successful run.</summary>
		public int Omitted { get; }

		/// <summary>When the processor's execution started.</summary>
		public DateTimeOffset? StartedAt { get; }

		/// <summary>When the processor's execution ended.</summary>
		public DateTimeOffset? CompletedAt { get; }

		/// <summary>
		/// Reads the run summary from a run's captured output: the last non-empty line, when it is a summary written by the
		/// Distenka SDK (it carries <c>distenkaSummary</c>). Returns <see langword="null"/> for output without one — a
		/// processor built with an older SDK, a processor whose own JSON happens to be the last line, a run that was killed,
		/// or output cut at the platform's 2000-line cap.
		/// </summary>
		/// <param name="output">The run's output, e.g. <see cref="RunDetails.Output"/>.</param>
		public static RunOutputSummary? TryParse(string? output)
		{
			var line = LastLine(output);
			if (line is null || line.Length == 0 || line[0] != '{' || line.IndexOf("\"distenkaSummary\"", StringComparison.Ordinal) < 0)
				return null;

			try
			{
				var summary = JsonSerializer.Deserialize(line, DistenkaJsonContext.Default.RunOutputSummary);
				return summary != null && summary.Version >= 1 ? summary : null;
			}
			catch (JsonException)
			{
				return null;
			}
		}

		static string? LastLine(string? output)
		{
			if (output is not string text)
				return null;

			var end = text.Length;
			while (end > 0 && char.IsWhiteSpace(text[end - 1]))
				end--;

			if (end == 0)
				return null;

			var start = text.LastIndexOf('\n', end - 1) + 1;
			return text.Substring(start, end - start).Trim();
		}

		static RunDisposition ParseDisposition(string? name)
		{
			if (name is not string value)
				return RunDisposition.Unknown;

			var word = value.Trim();
			if (string.Equals(word, "Successful", StringComparison.OrdinalIgnoreCase))
				return RunDisposition.Successful;
			if (string.Equals(word, "Failed", StringComparison.OrdinalIgnoreCase))
				return RunDisposition.Failed;
			if (string.Equals(word, "Cancelled", StringComparison.OrdinalIgnoreCase))
				return RunDisposition.Cancelled;
			return RunDisposition.Unknown;
		}
	}

	/// <summary>One item category of a <see cref="RunOutputSummary"/>.</summary>
	public sealed class RunOutputCategory
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunOutputCategory(string? category, int count, bool successful)
		{
			Category = category;
			Count = count;
			Successful = successful;
		}

		/// <summary>The category the processor gave its items (<c>(omitted)</c> when hidden on a successful run).</summary>
		public string? Category { get; }

		/// <summary>How many items ended in this category.</summary>
		public int Count { get; }

		/// <summary>Whether the items in this category succeeded.</summary>
		public bool Successful { get; }
	}

	/// <summary>One failed item of a <see cref="RunOutputSummary"/>.</summary>
	public sealed class RunOutputFailure
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunOutputFailure(string? id, string? category, string? message)
		{
			Id = id;
			Category = category;
			Message = message;
		}

		/// <summary>The item's id.</summary>
		public string? Id { get; }

		/// <summary>The item's category.</summary>
		public string? Category { get; }

		/// <summary>Why the item failed (the exception message); only present when the run did not succeed.</summary>
		public string? Message { get; }
	}
}
