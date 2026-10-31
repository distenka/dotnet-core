using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
    class RunAction : HostedAction
	{
        private readonly Config _config;
        private readonly IServiceProvider _serviceProvider;
		private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<RunAction> _logger;
		private readonly bool _debug;

		internal Config Config => _config;

		public RunAction(Config config, IHostApplicationLifetime applicationLifetime, IServiceProvider serviceProvider, ILogger<RunAction> logger, Debug debug)
		{
			_serviceProvider = serviceProvider;
			_applicationLifetime = applicationLifetime;
			this._config = config;
			this._logger = logger;
			this._debug = debug?.AttachDebugger ?? config?.Execution?.LaunchDebugger ?? false;
		}

		protected override async Task RunAsync(CancellationToken token)
		{
			// ADR-004 §2.1: pessimistic default. Every path that does not reach a clean Successful end — an exception,
			// a host stop that outlives the shutdown timeout, a crash in the reporter — leaves exit code 1.
			// Only a Successful disposition with a completed success path sets 0. Setting Environment.ExitCode (rather than
			// returning a code) keeps every existing `async Task Main` → RunProcessorAsync() processor unchanged.
			Environment.ExitCode = RunOutcome.FailureExitCode;

			string errorLine = null;

			try
			{
				var scope = _serviceProvider.CreateAsyncScope();
				try
				{
					errorLine = await ExecuteAndReportAsync(scope.ServiceProvider, token);
				}
				finally
				{
					await scope.DisposeAsync();
				}
			}
			catch (Exception ex)
			{
				// Replaces the former empty `catch { }` that turned every failure into exit 0 (ADR-004 §1.3).
				errorLine = RunOutcome.DescribeUnhandled(ex, token.IsCancellationRequested);
			}
			finally
			{
				try
				{
					if (errorLine is null)
					{
						Environment.ExitCode = RunOutcome.SuccessExitCode;
					}
					else
					{
						Environment.ExitCode = RunOutcome.FailureExitCode;

						// One physical ERROR: line on stdout carries the reason (ADR-004 §2.2/§2.3 rule 3). Written with
						// Console.Out, not AnsiConsole, so it is never wrapped or interpreted as markup, and it is written
						// even with --silent: it is part of the executor contract, not UI. Never stderr (§2.3 rule 1).
						Console.Out.WriteLine(errorLine);
					}

					// Ensure the entire output can be read by the node
					await Console.Out.FlushAsync();
				}
				finally
				{
					_applicationLifetime?.StopApplication();
				}
			}
		}

		/// <summary>
		/// Runs the execution and renders it. Returns the <c>ERROR:</c> line describing why the run did not succeed,
		/// or <c>null</c> when it ended <see cref="Disposition.Successful"/>.
		/// </summary>
		private async Task<string> ExecuteAndReportAsync(IServiceProvider services, CancellationToken token)
		{
			var execution = services.GetRequiredService<Execution>();

			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;

			AnsiConsole.MarkupLine($"Running [blue]{Markup.Escape(_config.Process.Type ?? string.Empty)}[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[dim]Press Ctrl+C to cancel[/]");
			AnsiConsole.WriteLine();

			if (_debug)
				AttachDebugger();

			var ProcessCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
			ResultLog log = null;

			if (_config.Execution.ResultsToConsole || _config.Execution.ResultsToFile)
				log = new ResultLog(execution);

			var executing = execution.ExecuteAsync(ProcessCancellation.Token);

			if (_config.Execution.ResultsToConsole)
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

			// Execution.RunAsync will ensure all event handlers have completed before exiting
			await executing;

			if (_config.Execution.ResultsToConsole)
			{
				AnsiConsole.WriteLine();

				if (execution.Disposition == Disposition.Successful)
					AnsiConsole.MarkupLine($"[bold green]Processor SUCCESSFUL[/]");
				else if (execution.Disposition == Disposition.Cancelled)
					AnsiConsole.MarkupLine($"[bold darkorange]Processor CANCELLED[/]");
				else
					AnsiConsole.MarkupLine($"[bold red invert]Processor FAILED[/]");
			}

			if (_config.Execution.ResultsToFile)
			{
				// Full detail (exception types, messages, stacks) belongs in the results file, not on stdout (ADR-004 §2.3 rule 3).
				using var writer = new StreamWriter(File.Open(_config.Execution.ResultsFilePath, FileMode.Create));

				writer.WriteJson(log);
				await writer.FlushAsync();
			}

			return RunOutcome.DescribeFailure(execution, token.IsCancellationRequested);
		}

		/// <summary>
		/// Writes the item categories and failed item ids. ADR-004 §2.2 row 2 / §2.3 rule 2: tolerated item failures must not
		/// fail a Successful run on the platform, so no exception type, message or stack is written here, and user text that
		/// would trip the executor's error heuristics (for example a category named after an exception type) is omitted.
		/// </summary>
		private static void WriteItemResults(ResultLog log)
		{
			const int MaxFailedItemLines = 10;

			var omitted = 0;

			string Safe(string text)
			{
				if (RunOutcome.ContainsErrorTrigger(text))
				{
					omitted++;
					return "(omitted)";
				}

				return RunOutcome.ToSingleLine(text);
			}

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

		private void AttachDebugger()
		{
			try
			{
				if (!Debugger.IsAttached)
				{
					Console.Write("Launching debugger...");

					if (Debugger.Launch() && Debugger.IsAttached)
						Console.WriteLine("attached.");
					else
						Console.WriteLine("failed to attach, continuing without debugger.");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to launch debugger due to error:\n" + ex.ToString());
			}
		}
	}

    internal class ValueColumn : ProgressColumn
    {
		protected override bool NoWrap => true;

        /// <summary>
        /// Gets or sets the style of the remaining time text.
        /// </summary>
        public Style Style { get; set; } = Color.Blue;

        /// <inheritdoc/>
        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            if (task.IsIndeterminate)
	            return new Text($"{task.Value:N0}", Style ?? Style.Plain);
			else
                return new Text($"{task.Value:N0}/{task.MaxValue:N0}", Style ?? Style.Plain);
        }
    }

    internal class AverageTimeColumn : ProgressColumn
    {
		private readonly Execution _execution;

		public AverageTimeColumn(Execution execution)
		{
			_execution = execution;
		}

        protected override bool NoWrap => true;

        /// <summary>
        /// Gets or sets the style of the remaining time text.
        /// </summary>
        public Style Style { get; set; } = Color.Blue;

        /// <inheritdoc/>
        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            return new Markup($"[blue]{_execution.ItemProcessingTime.AllTime.Count:N0}[/] [dim]({_execution.ItemProcessingTime.AllTime.Average.TotalMilliseconds}ms avg)[/]");
        }
    }

    internal class AverageTimeLastMinColumn : ProgressColumn
    {
        private readonly Execution _execution;

        public AverageTimeLastMinColumn(Execution execution)
        {
            _execution = execution;
        }

        protected override bool NoWrap => true;

        /// <summary>
        /// Gets or sets the style of the remaining time text.
        /// </summary>
        public Style Style { get; set; } = Color.Blue;

        /// <inheritdoc/>
        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
			return new Markup($"Last min: [blue]{_execution.ItemProcessingTime.LastMinuteAverage.Count:N0}[/] [dim]({_execution.ItemProcessingTime.LastMinuteAverage.Average.TotalMilliseconds}ms avg)[/]");
        }
    }
}
