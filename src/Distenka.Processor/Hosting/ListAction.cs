using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Hosting
{
	/// <summary>
	/// <c>list</c> (ADR-003 §2.1, §2.6; DCS-043):
	/// <list type="bullet">
	///   <item><description>no flags — the executor contract: exactly one compact JSON descriptor object on stdout, exit 0; or
	///   nothing on stdout, <c>ERROR:</c> lines on stderr, exit 1;</description></item>
	///   <item><description><c>--verbose</c> — a human-readable listing for developers;</description></item>
	///   <item><description><c>--legacy-json</c> — the Runly listing <c>{ clientVersion, Processes }</c>, kept for one major version;</description></item>
	///   <item><description><c>--json</c> — withdrawn: exits 1 with an <c>ERROR:</c> pointing to the replacements.</description></item>
	/// </list>
	/// </summary>
	internal class ListAction : HostedAction
	{
		/// <summary>Maximum number of <c>ERROR:</c> reasons written before a count (ADR-003 §2.1 rule 3).</summary>
		public const int MaxErrorLines = 10;

		private readonly bool _verbose;
		private readonly bool _legacyJson;
		private readonly bool _json;
		private readonly ProcessCache _cache;
		private readonly IHostApplicationLifetime _applicationLifetime;

		public ListAction(bool verbose, bool legacyJson, bool json, ProcessCache cache, IHostApplicationLifetime applicationLifetime)
		{
			_verbose = verbose;
			_legacyJson = legacyJson;
			_json = json;
			_cache = cache ?? throw new ArgumentNullException(nameof(cache));
			_applicationLifetime = applicationLifetime;
		}

		protected override async Task RunAsync(CancellationToken cancel)
		{
			// Pessimistic default, as for `run` (ADR-004 §2.1): only a completed description exits 0.
			Environment.ExitCode = 1;

			try
			{
				Environment.ExitCode = Execute(Console.Out, Console.Error);

				// Ensure the entire output can be read by the node
				await Console.Out.FlushAsync();
				await Console.Error.FlushAsync();
			}
			catch (Exception ex)
			{
				Environment.ExitCode = 1;
				Console.Error.WriteLine(RunOutcome.ErrorLine($"list failed: {ex.GetType().FullName}: {ex.Message}"));
			}
			finally
			{
				_applicationLifetime?.StopApplication();
			}
		}

		/// <summary>Writes the requested listing and returns the exit code.</summary>
		internal int Execute(TextWriter stdout, TextWriter stderr)
		{
			if (_json)
			{
				stderr.WriteLine("ERROR: list --json was withdrawn: use `list` for the platform input schema, `list --verbose` for a readable listing or `list --legacy-json` for the 1.x JSON listing");
				return 1;
			}

			if (_legacyJson)
			{
				WriteLegacyJson(stdout);
				return 0;
			}

			var result = DescriptorSchemaGenerator.Generate(_cache.Processes);

			if (_verbose)
			{
				WriteVerbose(stdout, result);
				return result.Success ? 0 : 1;
			}

			foreach (var warning in result.Warnings)
				stderr.WriteLine(RunOutcome.ToSingleLine("WARN: " + warning));

			if (!result.Success)
			{
				WriteErrors(stderr, result.Errors);
				return 1;
			}

			// Exactly one JSON object and a single newline — nothing else on stdout (ADR-003 §2.1 rule 1).
			stdout.Write(result.Json);
			stdout.Write('\n');
			return 0;
		}

		static void WriteErrors(TextWriter stderr, IReadOnlyList<string> errors)
		{
			foreach (var error in errors.Take(MaxErrorLines))
				stderr.WriteLine(RunOutcome.ErrorLine(error));

			if (errors.Count > MaxErrorLines)
				stderr.WriteLine($"ERROR: … and {errors.Count - MaxErrorLines} more");
		}

		void WriteVerbose(TextWriter writer, DescriptorSchemaResult result)
		{
			writer.WriteLine($"Distenka {ClientVersion()}");
			writer.WriteLine();

			foreach (var process in _cache.Processes.OrderBy(p => p.ProcessType.FullName, StringComparer.Ordinal))
			{
				writer.WriteLine(ConsoleFormat.DoubleLine);
				writer.WriteLine($"Process:\t{process.ProcessType.FullName} [{process.ProcessType.Assembly.GetName().Name}]");
				writer.WriteLine($"Config:\t{process.ConfigType?.FullName} [{process.ConfigType?.Assembly.GetName().Name}]");
				if (!process.IsValid)
					writer.WriteLine($"Not runnable: {process.Errors}");
				writer.WriteLine();
			}

			writer.WriteLine(ConsoleFormat.DoubleLine);

			if (result.Success)
			{
				writer.WriteLine("Input schema (what `list` prints for the platform):");
				writer.WriteLine(result.Schema.ToString(Formatting.Indented));
			}
			else
			{
				writer.WriteLine("The package cannot be described for the platform:");
				foreach (var error in result.Errors)
					writer.WriteLine("  " + error);
			}

			foreach (var warning in result.Warnings)
				writer.WriteLine("Warning: " + warning);
		}

		void WriteLegacyJson(TextWriter writer)
		{
			var Processes = _cache.Processes.OrderBy(i => i.ProcessType.FullName).Select(p => new
			{
				ProcessType = p.ProcessType.FullName,
				ConfigType = p.ConfigType.FullName,
				DefaultConfig = p.IsValid ? ConfigWriter.ToReducedProcessject(_cache.GetDefaultConfig(p)) : null,
				Assembly = p.ProcessType.Assembly.GetName().Name,
				CanRun = p.IsValid,
				Errors = p.Errors.ToString()
			});

			writer.WriteJson(new { clientVersion = ClientVersion(), Processes });
			writer.WriteLine();
		}

		static string ClientVersion() =>
			typeof(ListAction).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
	}
}
