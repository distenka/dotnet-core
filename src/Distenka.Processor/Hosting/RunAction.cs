using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Diagnostics;
using System.IO;
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
		private readonly bool _forcePlain;
		private Execution _execution;
		private ResultLog _log;

		internal Config Config => _config;

		public RunAction(Config config, IHostApplicationLifetime applicationLifetime, IServiceProvider serviceProvider, ILogger<RunAction> logger, Debug debug, RunOutputOptions output = null)
		{
			_serviceProvider = serviceProvider;
			_applicationLifetime = applicationLifetime;
			this._config = config;
			this._logger = logger;
			this._debug = debug?.AttachDebugger ?? config?.Execution?.LaunchDebugger ?? false;
			this._forcePlain = output?.ForcePlain ?? false;
		}

		protected override async Task RunAsync(CancellationToken token)
		{
			// ADR-004 §2.1: pessimistic default. Every path that does not reach a clean Successful end — an exception,
			// a host stop that outlives the shutdown timeout, a crash in the reporter — leaves exit code 1.
			// Only a Successful disposition with a completed success path sets 0. Setting Environment.ExitCode (rather than
			// returning a code) keeps every existing `async Task Main` → RunProcessorAsync() processor unchanged.
			Environment.ExitCode = RunOutcome.FailureExitCode;

			// Under the executor stdout is always redirected, so the summary line (DCS-046) is always written there; on a
			// terminal the interactive UI shows the result instead. Independent of --silent: like the ERROR: line, the
			// summary is part of the executor contract, not UI.
			var plain = RunOutputOptions.IsPlain(_forcePlain, Console.IsOutputRedirected);
			if (plain)
				RunOutputOptions.UseUtf8Output();

			string errorLine = null;
			var cancelledByHost = false;

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
				cancelledByHost = ex is OperationCanceledException && token.IsCancellationRequested;
			}
			finally
			{
				try
				{
					var summary = plain ? Summarise(errorLine, cancelledByHost) : null;

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

					// The summary is the last stdout line (ADR-004 §2.4): after the ERROR: line, after every reporter and
					// logger line (both write synchronously before this point).
					if (summary != null)
						Console.Out.WriteLine(summary);

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
		/// Runs the execution and reports it through the reporter for the current output mode (ADR-009 §2.1, DCS-041).
		/// Returns the <c>ERROR:</c> line describing why the run did not succeed, or <c>null</c> when it ended
		/// <see cref="Disposition.Successful"/>.
		/// </summary>
		private async Task<string> ExecuteAndReportAsync(IServiceProvider services, CancellationToken token)
		{
			var execution = services.GetRequiredService<Execution>();
			_execution = execution;
			var reporter = CreateReporter();

			reporter.Starting(_config.Process.Type);

			if (_debug)
				AttachDebugger();

			var ProcessCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
			ResultLog log = null;

			// The summary (DCS-046) lists failed items and the FinalizeAsync output from the log, so plain mode keeps one too.
			if (_config.Execution.ResultsToConsole || _config.Execution.ResultsToFile || RunOutputOptions.IsPlain(_forcePlain, Console.IsOutputRedirected))
				log = new ResultLog(execution);

			_log = log;

			var executing = execution.ExecuteAsync(ProcessCancellation.Token);

			await reporter.MonitorAsync(execution, log, executing);

			// Execution.RunAsync will ensure all event handlers have completed before exiting
			await executing;

			reporter.Completed(execution);

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
		/// Builds the summary line for how the run ended (DCS-046, ADR-004 §2.2): from the execution when it completed,
		/// otherwise without counts (rows 5–6 when the pipeline did not finish). Never throws: a summary that cannot be built
		/// falls back to one without counts, and to none at all only if even that fails.
		/// </summary>
		private string Summarise(string errorLine, bool cancelledByHost)
		{
			var processType = _config?.Process?.Type;
			var execution = _execution;

			Disposition disposition;
			if (errorLine is null)
				disposition = Disposition.Successful;
			else if (execution?.IsComplete == true && execution.Disposition != Disposition.Successful)
				disposition = execution.Disposition;
			else
				disposition = cancelledByHost ? Disposition.Cancelled : Disposition.Failed;

			try
			{
				return execution?.IsComplete == true
					? RunSummary.ForExecution(execution, _log, processType, disposition)
					: RunSummary.ForIncompleteRun(processType, disposition);
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Building the run summary from the execution failed; writing it without counts");

				try
				{
					return RunSummary.ForIncompleteRun(processType, disposition);
				}
				catch (Exception)
				{
					return null;
				}
			}
		}

		private IRunReporter CreateReporter()
		{
			if (!_config.Execution.ResultsToConsole)
				return NullRunReporter.Instance;

			if (!RunOutputOptions.IsPlain(_forcePlain, Console.IsOutputRedirected))
				return new InteractiveSpectreReporter();

			RunOutputOptions.UseUtf8Output();
			return new PlainLineReporter(Console.Out);
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
