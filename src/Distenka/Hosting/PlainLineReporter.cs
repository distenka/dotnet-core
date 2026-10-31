using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// Plain-mode reporter (ADR-009 §2.1/§2.2, DCS-041): writes only <c>LEVEL: message</c> lines to stdout — no ANSI
	/// sequences, spinners or markup — and keeps the SDK's own lines bounded (ADR-004 §2.4): a start line, one line per
	/// lifecycle step, throttled progress, at most <see cref="MaxFailedItemLines"/> failed-item lines plus a count, and a
	/// final disposition line. Never writes <c>ERROR:</c> (that line belongs to <see cref="RunAction"/>) and never stderr.
	/// </summary>
	internal sealed class PlainLineReporter : IRunReporter
	{
		/// <summary>Default minimum interval between two progress lines.</summary>
		public static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromSeconds(30);

		/// <summary>Maximum number of failed items listed by id; further failures are reported as a count.</summary>
		public const int MaxFailedItemLines = 10;

		/// <summary>Maximum number of item categories listed; further categories are reported as a count.</summary>
		public const int MaxCategories = 10;

		readonly TextWriter _output;
		readonly TimeProvider _time;
		readonly TimeSpan _progressInterval;
		readonly TimeSpan _pollInterval;
		readonly object _lock = new object();
		bool _processingStarted;
		ResultLog _log;

		public PlainLineReporter(TextWriter output)
			: this(output, TimeProvider.System, DefaultProgressInterval, TimeSpan.FromMilliseconds(100)) { }

		public PlainLineReporter(TextWriter output, TimeProvider time, TimeSpan progressInterval, TimeSpan pollInterval)
		{
			_output = output ?? throw new ArgumentNullException(nameof(output));
			_time = time ?? throw new ArgumentNullException(nameof(time));
			_progressInterval = progressInterval;
			_pollInterval = pollInterval;
		}

		public void Starting(string processType)
		{
			var omitted = 0;
			Info($"Starting {ReporterText.Safe(processType ?? "processor", ref omitted)}");
		}

		public async Task MonitorAsync(Execution execution, ResultLog log, Task executing)
		{
			if (execution is null)
				throw new ArgumentNullException(nameof(execution));

			executing ??= Task.CompletedTask;
			_log = log;

			await WaitWhileAsync(() => execution.State == ExecutionState.NotStarted || execution.State == ExecutionState.Initializing, executing);

			if (execution.InitializeAsync?.IsSuccessful == true)
			{
				Info("Initialized");

				await WaitWhileAsync(() => execution.State == ExecutionState.GettingItemsToProcess, executing);

				if (execution.GetItemsAsync?.IsSuccessful == true &&
					execution.GetEnumerator?.IsSuccessful != false &&
					execution.Count?.IsSuccessful != false)
				{
					_processingStarted = true;

					Info(execution.TotalItemCount.HasValue
						? $"Processing {execution.TotalItemCount.Value} items"
						: "Processing items (total not known in advance)");

					var throttle = new ProgressThrottle(_time, _progressInterval);

					while (execution.State == ExecutionState.Processing && !executing.IsCompleted)
					{
						if (throttle.ShouldReport())
							Info(Progress(execution));

						await Task.Delay(_pollInterval);
					}
				}
			}

		}

		/// <remarks>
		/// Final counts and item results are written here, not at the end of <see cref="MonitorAsync"/>: only after the
		/// execution task has completed have all item events reached the <see cref="ResultLog"/>.
		/// </remarks>
		public void Completed(Execution execution)
		{
			if (_processingStarted)
				Info(Progress(execution));

			WriteItemResults(_log);

			if (execution.FinalizeAsync?.IsSuccessful == true)
				Info("Finalized");

			Info($"Disposition={execution.Disposition}");

			lock (_lock)
				_output.Flush();
		}

		static string Progress(Execution execution) =>
			(execution.TotalItemCount.HasValue
				? $"Processed {execution.CompletedItemCount}/{execution.TotalItemCount.Value} items"
				: $"Processed {execution.CompletedItemCount} items") +
			$" ({execution.SuccessfulItemCount} successful, {execution.FailedItemCount} failed)";

		void WriteItemResults(ResultLog log)
		{
			if (log is null)
				return;

			var omitted = 0;

			var categories = log.Categories.ToList();
			if (categories.Count > 0)
			{
				var listed = categories.Take(MaxCategories)
					.Select(c => $"{c.Count} {ReporterText.Safe(c.Category ?? (c.IsSuccessful ? Result.Successful : Result.Failed), ref omitted)}{(c.IsSuccessful ? "" : " [failed]")}");

				var more = categories.Count > MaxCategories ? $", +{categories.Count - MaxCategories} more categories" : "";

				Info($"Item results: {string.Join(", ", listed)}{more}");
			}

			var failed = log.FailedItems.ToList();

			foreach (var failure in failed.Take(MaxFailedItemLines))
				Warn($"item '{ReporterText.Safe(failure.Id ?? "unknown", ref omitted)}' failed");

			if (failed.Count > MaxFailedItemLines)
				Warn($"{failed.Count - MaxFailedItemLines} more failed item(s) not listed");

			if (omitted > 0)
				Warn($"{omitted} item message(s) omitted from output");
		}

		async Task WaitWhileAsync(Func<bool> condition, Task executing)
		{
			while (condition() && !executing.IsCompleted)
				await Task.Delay(_pollInterval);
		}

		void Info(string message) => Write("INFO: " + message);

		void Warn(string message) => Write("WARN: " + message);

		void Write(string line)
		{
			// One complete physical line per write, under a lock (ADR-009 §2.2).
			line = RunOutcome.ToSingleLine(line);

			lock (_lock)
				_output.WriteLine(line);
		}
	}

	/// <summary>Allows at most one progress line per interval (DCS-041).</summary>
	internal sealed class ProgressThrottle
	{
		readonly TimeProvider _time;
		readonly TimeSpan _interval;
		DateTimeOffset _last;

		public ProgressThrottle(TimeProvider time, TimeSpan interval)
		{
			_time = time ?? throw new ArgumentNullException(nameof(time));
			_interval = interval;
			_last = time.GetUtcNow();
		}

		/// <summary>True when at least one interval has passed since the start or since the last <c>true</c>.</summary>
		public bool ShouldReport()
		{
			var now = _time.GetUtcNow();

			if (now - _last < _interval)
				return false;

			_last = now;
			return true;
		}
	}
}
