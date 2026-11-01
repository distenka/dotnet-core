using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Distenka.Hosting;

namespace Distenka
{
	/// <summary>
	/// Provides convenience methods for creating instances of <see cref="IHostBuilder"/> with pre-configured defaults.
	/// </summary>
	public static class ProcessorHost
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HostBuilder"/> class with pre-configured defaults.
		/// </summary>
		/// <remarks>
		///   The following defaults are applied to the returned <see cref="HostBuilder"/>:
		///   <list type="bullet">
		///     <item><description>set the <see cref="IHostEnvironment.ContentRootPath"/> to the result of <see cref="Directory.GetCurrentDirectory()"/></description></item>
		///     <item><description>load host <see cref="IConfiguration"/> from "DOTNET_" prefixed environment variables</description></item>
		///     <item><description>load host <see cref="IConfiguration"/> from supplied command line args</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from 'appsettings.json' and 'appsettings.[<see cref="IHostEnvironment.EnvironmentName"/>].json'</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from User Secrets when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development' using the entry assembly</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from environment variables</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from supplied command line args</description></item>
		///     <item><description>configure the <see cref="ILoggerFactory"/> to write one executor-compatible line per event to stdout for <c>run</c> and processor commands (ADR-009; not for <c>list</c>/<c>get</c>), plus debug and event log output when stdout is a terminal, and event source output always</description></item>
		///     <item><description>enables scope validation on the dependency injection container when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development'</description></item>
		///   </list>
		/// </remarks>
		/// <param name="args">The command line args.</param>
		/// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
		public static IHostBuilder CreateDefaultBuilder(string[] args)
		{
			return CreateDefaultBuilder(args, Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HostBuilder"/> class with pre-configured defaults.
		/// </summary>
		/// <remarks>
		///   The following defaults are applied to the returned <see cref="HostBuilder"/>:
		///   <list type="bullet">
		///     <item><description>set the <see cref="IHostEnvironment.ContentRootPath"/> to the result of <see cref="Directory.GetCurrentDirectory()"/></description></item>
		///     <item><description>load host <see cref="IConfiguration"/> from "DOTNET_" prefixed environment variables</description></item>
		///     <item><description>load host <see cref="IConfiguration"/> from supplied command line args</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from 'appsettings.json' and 'appsettings.[<see cref="IHostEnvironment.EnvironmentName"/>].json'</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from User Secrets when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development' using the entry assembly</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from environment variables</description></item>
		///     <item><description>load app <see cref="IConfiguration"/> from supplied command line args</description></item>
		///     <item><description>configure the <see cref="ILoggerFactory"/> to write one executor-compatible line per event to stdout for <c>run</c> and processor commands (ADR-009; not for <c>list</c>/<c>get</c>), plus debug and event log output when stdout is a terminal, and event source output always</description></item>
		///     <item><description>enables scope validation on the dependency injection container when <see cref="IHostEnvironment.EnvironmentName"/> is 'Development'</description></item>
		///   </list>
		/// </remarks>
		/// <param name="args">The command line args.</param>
		/// <param name="ProcessAssemblies">The assemblies in which to search for <see cref="IProcessor"/>s.</param>
		/// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
		public static IHostBuilder CreateDefaultBuilder(string[] args, params Assembly[] ProcessAssemblies)
		{
			args ??= Array.Empty<string>();

			// ADR-009 §2.1/§2.3: commands that run processor code (`run` and the per-processor commands) write ILogger events
			// to stdout as executor-compatible lines. `list` and `get` must keep stdout for their own document (ADR-003 §2.1).
			var command = args.Length > 0 ? args[0] : null;
			var writesLogsToStdout = command != "list" && command != "get";
			var plain = RunOutputOptions.IsPlain(args.Contains("--plain") || args.Contains("-p"), Console.IsOutputRedirected);

			if (writesLogsToStdout && plain)
				RunOutputOptions.UseUtf8Output();

			return Host
				.CreateDefaultBuilder()
				.ConfigureServices((hostingContext, services) =>
				{
					if (writesLogsToStdout)
					{
						// The host's "Application started. Press Ctrl+C to shut down." / environment / content-root lines are noise
						// in a run (ADR-009 §2.5).
						services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
					}

					services.AddLogging(logging =>
					{
						// We need to remove the console logger that Host.CreateDefaultBuilder()
						// adds, so we'll ClearProviders then redo everything, minus the console logger.
						logging.ClearProviders();

						bool isWindows =
#if NETCOREAPP
                    OperatingSystem.IsWindows();
#elif NETFRAMEWORK
                    Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

						// IMPORTANT: This needs to be added *before* configuration is loaded, this lets
						// the defaults be overridden by the configuration.
						if (isWindows)
						{
							// Default the EventLogLoggerProvider to warning or above
							logging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Warning);
						}

						if (writesLogsToStdout)
						{
							// ADR-009 §2.5 defaults; the package's appsettings.json `Logging` section, loaded next, overrides them.
							logging.SetMinimumLevel(LogLevel.Information);
							logging.AddFilter("Microsoft", LogLevel.Warning);
							logging.AddFilter("System", LogLevel.Warning);
							logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
						}

						logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

						if (writesLogsToStdout)
						{
							logging.AddProvider(new StdoutLineLoggerProvider(new StdoutLineLoggerOptions
							{
								Plain = plain,
								NeutraliseHeuristicTriggers = hostingContext.Configuration.GetValue<bool?>(StdoutLineLoggerOptions.NeutraliseHeuristicTriggersKey) ?? true
							}));
						}

						// Plain mode (the sandbox): stdout lines + EventSource only (ADR-009 §2.3).
						var sandboxed = writesLogsToStdout && plain;

						if (!sandboxed)
							logging.AddDebug();

						logging.AddEventSourceLogger();

						if (isWindows && !sandboxed)
						{
							// Add the EventLogLoggerProvider on windows machines
							logging.AddEventLog();
						}

						logging.Configure(options =>
						{
							options.ActivityTrackingOptions =
								ActivityTrackingOptions.SpanId |
								ActivityTrackingOptions.TraceId |
								ActivityTrackingOptions.ParentId;
						});
					});

					services.AddDistenkaProcessor(args, ProcessAssemblies);

				});
		}

		/// <summary>
		/// Runs the Processor.
		/// </summary>
		/// <param name="host">The <see cref="IHost"/> to run.</param>
		/// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public static Task RunProcessorAsync(this IHost host) => host.RunAsync();

        /// <summary>
        /// Runs the Processor.
        /// </summary>
        /// <param name="host">The <see cref="IHost"/> to run.</param>
        /// <param name="cancellationToken">The token to trigger cancellation.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public static Task RunProcessorAsync(this IHost host, CancellationToken cancellationToken) => 
			host.RunAsync(cancellationToken);
    }
}
