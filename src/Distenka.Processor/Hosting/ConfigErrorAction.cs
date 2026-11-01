using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// Replaces <see cref="RunAction"/> when the configuration cannot be read or resolved (ADR-004 §2.2 row 7,
	/// ADR-002 §2.4, DCS-044): writes one <c>ERROR:</c> line to stdout, nothing to stderr, and exits with code 1. In plain
	/// mode the run summary follows as the last line, with <c>disposition: Failed</c> and no counts (DCS-046).
	/// </summary>
	internal sealed class ConfigErrorAction : HostedAction
	{
		readonly string _reason;
		readonly IHostApplicationLifetime _applicationLifetime;
		readonly bool _forcePlain;

		public ConfigErrorAction(string reason, IHostApplicationLifetime applicationLifetime, RunOutputOptions output = null)
		{
			_reason = reason;
			_applicationLifetime = applicationLifetime;
			_forcePlain = output?.ForcePlain ?? false;
		}

		protected override async Task RunAsync(CancellationToken cancellation)
		{
			try
			{
				Environment.ExitCode = RunOutcome.FailureExitCode;
				Console.Out.WriteLine(RunOutcome.ErrorLine(_reason));

				if (RunOutputOptions.IsPlain(_forcePlain, Console.IsOutputRedirected))
				{
					// The configuration could not be read, so the process it names is unknown.
					Console.Out.WriteLine(RunSummary.ForIncompleteRun(processType: null, Disposition.Failed));
				}
				await Console.Out.FlushAsync();
			}
			finally
			{
				_applicationLifetime?.StopApplication();
			}
		}
	}
}
