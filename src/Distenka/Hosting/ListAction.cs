using Microsoft.Extensions.Hosting;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	internal class ListAction : HostedAction
	{
		private readonly bool _verbose;
		private readonly bool _json;
		private readonly ProcessCache _cache;
		private readonly JsonSchema _schema;
		private readonly IHostApplicationLifetime _applicationLifetime;

		public ListAction(bool verbose, bool json, ProcessCache cache, JsonSchema schema, IHostApplicationLifetime applicationLifetime)
		{
			_verbose = verbose;
			_json = json;
			_cache = cache ?? throw new ArgumentNullException(nameof(cache));
			_schema = schema ?? throw new ArgumentNullException(nameof(schema));
			_applicationLifetime = applicationLifetime;
		}

		protected override async Task RunAsync(CancellationToken cancel)
		{
			try
			{
				string clientVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

				if (_json)
				{
					WriteJson(Console.Out, clientVersion);
				}
				else
				{
					WritePlainText(Console.Out, clientVersion);
				}

				// Ensure the entire output can be read by the node
				Console.WriteLine();
				await Console.Out.FlushAsync();
            }
			finally
			{
				_applicationLifetime?.StopApplication();
			}
        }

		void WriteJson(TextWriter writer, string clientVersion)
		{
			var Processes = GetProcessJson();
			writer.WriteJson(new { clientVersion, Processes });
		}

		IEnumerable GetProcessJson()
		{
			if (_verbose)
			{
				return _cache.Processes.OrderBy(i => i.ProcessType.FullName).Select(p => new
				{
					ProcessType = p.ProcessType.FullName,
					ConfigType = p.ConfigType.FullName,
					DefaultConfig = _cache.GetDefaultConfig(p.ProcessType.FullName),
					Assembly = p.ProcessType.Assembly.GetName().Name,
					CanRun = p.IsValid,
					Errors = p.Errors.ToString(),
					Schema = _schema.Generate(p.ConfigType)
				});
			}

			// Include the reduced config
			return _cache.Processes.OrderBy(i => i.ProcessType.FullName).Select(p => new
			{
				ProcessType = p.ProcessType.FullName,
				ConfigType = p.ConfigType.FullName,
				DefaultConfig = ConfigWriter.ToReducedProcessject(_cache.GetDefaultConfig(p.ProcessType.FullName)),
				Assembly = p.ProcessType.Assembly.GetName().Name,
				CanRun = p.IsValid,
				Errors = p.Errors.ToString()
			});
		}

		void WritePlainText(TextWriter writer, string clientVersion)
		{
			writer.WriteLine($"BroadCast Version: v{clientVersion}");
			writer.WriteLine();

			foreach (var Process in _cache.Processes.OrderBy(i => i.ProcessType.FullName))
			{
				writer.WriteLine(ConsoleFormat.DoubleLine);
				writer.WriteLine($"Process:\t{Process.ProcessType.FullName} [{Process.ProcessType.Assembly.GetName().Name}]");
				writer.WriteLine($"Config:\t{Process.ConfigType.FullName} [{Process.ConfigType.Assembly.GetName().Name}]");
				writer.WriteLine(ConsoleFormat.DoubleLine);
				writer.WriteLine();

				if (Process.IsValid)
				{
					writer.WriteLine(_verbose ? ConfigWriter.ToJson(_cache.GetDefaultConfig(Process)) : ConfigWriter.ToReducedJson(_cache.GetDefaultConfig(Process)));
					writer.WriteLine();
				}
				else
				{
					writer.WriteLine($"Error Loading Process: {Process.Errors.ToString()}");
					writer.WriteLine();
				}
			}
		}
	}
}
