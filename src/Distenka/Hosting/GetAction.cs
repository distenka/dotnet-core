using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	internal class GetAction : HostedAction
	{
		private readonly bool _verbose;
		private string _filePath;
		private readonly string _ProcessType;
		private readonly ProcessCache _cache;
		private readonly IHostApplicationLifetime _applciationLifetime;

		internal GetAction(bool verbose, string ProcessType, string filePath, ProcessCache cache, IHostApplicationLifetime applicationLifetime)
		{
			_verbose = verbose;
			_ProcessType = ProcessType;
			_filePath = filePath;
			_cache = cache;
			_applciationLifetime = applicationLifetime;
		}

        protected override async Task RunAsync(CancellationToken cancel)
		{
			try
			{
				TextWriter writer = null;
				ProcessInfo Process;

				try
				{
					Process = _cache.Get(_ProcessType);
				}
				catch (TypeNotFoundException)
				{
					Console.WriteLine($"Could not find the Process type '{_ProcessType}'.");
					return;
				}

				var config = _cache.GetDefaultConfig(Process);

				try
				{

					if (_filePath == null)
					{
						writer = Console.Out;
					}
					else
					{
						// If path is an existing directory, such as ".", add a file name
						if (Directory.Exists(_filePath))
							_filePath = Path.Combine(_filePath, Process.ProcessType.Name + ".json");

						writer = new StreamWriter(File.Open(_filePath, FileMode.Create));
					}

					await writer.WriteAsync(_verbose ? ConfigWriter.ToJson(config) : ConfigWriter.ToReducedJson(config));
				}
				finally
				{
					if (_filePath != null && writer != null)
					{
						await writer.FlushAsync();
						writer.Dispose();
					}
				}

				if (_filePath != null)
					Console.WriteLine($"Default config for {Process.ProcessType.FullName} saved to {Path.GetFullPath(_filePath)}");
			}
			finally
			{
				_applciationLifetime?.StopApplication();
			}
		}
	}
}
