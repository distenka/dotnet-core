using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// Replaces <see cref="RunAction"/> when the configuration cannot be read or resolved (ADR-004 §2.2 row 7,
	/// ADR-002 §2.4, DCS-044): writes one <c>ERROR:</c> line to stdout, nothing to stderr, and exits with code 1.
	/// </summary>
	internal sealed class ConfigErrorAction : HostedAction
	{
		readonly string _reason;
		readonly IHostApplicationLifetime _applicationLifetime;

		public ConfigErrorAction(string reason, IHostApplicationLifetime applicationLifetime)
		{
			_reason = reason;
			_applicationLifetime = applicationLifetime;
		}

		protected override async Task RunAsync(CancellationToken cancellation)
		{
			try
			{
				Environment.ExitCode = RunOutcome.FailureExitCode;
				Console.Out.WriteLine(RunOutcome.ErrorLine(_reason));
				await Console.Out.FlushAsync();
			}
			finally
			{
				_applicationLifetime?.StopApplication();
			}
		}
	}
}
