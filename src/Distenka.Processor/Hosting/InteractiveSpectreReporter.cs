using Spectre.Console;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// The Spectre.Console UI (spinners, progress bar, markup) used when stdout is a terminal (ADR-009 §2.1 interactive mode).
	/// Behaviour is the pre-DCS-041 <see cref="RunAction"/> rendering, moved here unchanged.
	/// </summary>
	internal sealed class InteractiveSpectreReporter : IRunReporter
	{
		public void Starting(string processType)
		{
			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;

			AnsiConsole.MarkupLine($"Running [blue]{Markup.Escape(processType ?? string.Empty)}[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[dim]Press Ctrl+C to cancel[/]");
			AnsiConsole.WriteLine();
		}

		public async Task MonitorAsync(Execution execution, ResultLog log, Task executing)
		{
			await AnsiConsole.Status()
				.Spinner(Spinner.Known.Dots)
				.SpinnerStyle(Style.Parse("yellow bold"))
				.StartAsync("Running", async ctx =>
				{
					AnsiConsole.Markup("Initializing...");

					while (execution.State == ExecutionState.NotStarted ||
						execution.State == ExecutionState.Initializing)
						await Task.Delay(10);

					if (execution.InitializeAsync.IsSuccessful)
					{
						AnsiConsole.MarkupLine("[green]Success[/]");

						AnsiConsole.Markup("Getting items...");

						while (execution.State == ExecutionState.GettingItemsToProcess)
							await Task.Delay(10);

						if (execution.GetItemsAsync.IsSuccessful && execution.GetEnumerator.IsSuccessful && execution.Count.IsSuccessful)
						{
							AnsiConsole.MarkupLine("[green]Success[/]");
						}
						else
						{
							AnsiConsole.MarkupLine("[red]Failed[/]");
							AnsiConsole.WriteLine();
							AnsiConsole.WriteException(execution.GetItemsAsync.Exception ?? execution.GetEnumerator.Exception ?? execution.Count.Exception);
							AnsiConsole.WriteLine();
						}
					}
					else
					{
						AnsiConsole.MarkupLine("[red]Failed[/]");
						AnsiConsole.WriteLine();
						AnsiConsole.WriteException(execution.InitializeAsync.Exception);
						AnsiConsole.WriteLine();
					}

				});

			if (execution.Disposition != Disposition.Failed)
			{
				var isIndeterminate = !execution.TotalItemCount.HasValue;

				var cols = isIndeterminate ?
				new ProgressColumn[]
				{
					new TaskDescriptionColumn(),
					new AverageTimeColumn(execution),
					new AverageTimeLastMinColumn(execution),
					new ElapsedTimeColumn() { Style = Style.Parse("dim") },
					new SpinnerColumn(Spinner.Known.Dots),
				} :
				new ProgressColumn[]
				{
					new TaskDescriptionColumn(),
					new AverageTimeColumn(execution),
					new AverageTimeLastMinColumn(execution),
					new PercentageColumn(),
					new ElapsedTimeColumn() { Style = Style.Parse("dim") },
					new SpinnerColumn(Spinner.Known.Dots),
				};

				await AnsiConsole.Progress()
					.Columns(cols)
					.StartAsync(async ctx =>
					{
						var process = ctx.AddTask(isIndeterminate ? "Processing items:" : $"Processing {execution.TotalItemCount:N0} items:", maxValue: execution.TotalItemCount ?? double.MaxValue);

						process.IsIndeterminate = isIndeterminate;

						while (execution.State == ExecutionState.Processing)
						{
							process.Value = execution.CompletedItemCount;

							await Task.Delay(100);
						}

						await Task.Delay(100);
						ctx.Refresh();

						process.Value = execution.CompletedItemCount;
						process.StopTask();
					});
			}
			else
			{
				AnsiConsole.WriteLine();
			}

			await AnsiConsole.Status()
				.Spinner(Spinner.Known.Dots)
				.SpinnerStyle(Style.Parse("yellow bold"))
				.StartAsync("Running", async ctx =>
				{
					WriteItemResults(log);

					AnsiConsole.Markup("Finalizing...");

					while (execution.State == ExecutionState.Finalizing)
						await Task.Delay(10);

					if (execution.FinalizeAsync.IsSuccessful)
					{
						AnsiConsole.MarkupLine("[green]Success[/]");
					}
					else
					{
						AnsiConsole.MarkupLine("[red]Failed[/]");
						AnsiConsole.WriteLine();
						AnsiConsole.WriteException(execution.FinalizeAsync.Exception);
						AnsiConsole.WriteLine();
					}
				});
		}

		public void Completed(Execution execution)
		{
			AnsiConsole.WriteLine();

			if (execution.Disposition == Disposition.Successful)
				AnsiConsole.MarkupLine($"[bold green]Processor SUCCESSFUL[/]");
			else if (execution.Disposition == Disposition.Cancelled)
				AnsiConsole.MarkupLine($"[bold darkorange]Processor CANCELLED[/]");
			else
				AnsiConsole.MarkupLine($"[bold red invert]Processor FAILED[/]");
		}

		private static void WriteItemResults(ResultLog log)
		{
			const int MaxFailedItemLines = 10;

			var omitted = 0;

			string Safe(string text) => ReporterText.Safe(text, ref omitted);

			var categories = log.Categories.ToList();

			if (categories.Any())
			{
				AnsiConsole.MarkupLine("[dim]Item results:[/]");

				var grid = new Grid();
				grid.AddColumn();
				grid.AddColumn();

				foreach (var c in categories)
				{
					var name = Markup.Escape(Safe(c.Category ?? (c.IsSuccessful ? Result.Successful : Result.Failed)));

					grid.AddRow(new Markup[]
					{
						new Markup($"{c.Count}"),
						new Markup(c.IsSuccessful ? name : $"[red]{name}[/]")
					});
				}

				AnsiConsole.Write(grid);
				AnsiConsole.WriteLine();
			}

			var failed = log.FailedItems.ToList();

			if (failed.Count > 0)
			{
				foreach (var failure in failed.Take(MaxFailedItemLines))
					AnsiConsole.WriteLine($"WARN: item '{Safe(failure.Id ?? "unknown")}' failed");

				if (failed.Count > MaxFailedItemLines)
					AnsiConsole.WriteLine($"WARN: {failed.Count - MaxFailedItemLines} more failed item(s) not listed");

				AnsiConsole.WriteLine();
			}

			if (omitted > 0)
				AnsiConsole.WriteLine($"WARN: {omitted} item message(s) omitted from output");
		}
	}
}
