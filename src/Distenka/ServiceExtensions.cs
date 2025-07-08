using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Distenka.Hosting;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Distenka
{
	/// <summary>
	/// Extension methods for <see cref="IServiceCollection"/> to add services for Distenka Processes.
	/// </summary>
	public static class ServiceExtensions
	{
		const string ListCommand = "list";
		const string GetCommand = "get";
		const string RunCommand = "run";
		const string ConfigPathArgument = "configPath";

		/// <summary>
		/// Adds the services required to execute the Processor specified in <paramref name="config"/>. The Processor type must be found in the calling assembly.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="config">The <see cref="Config"/> that determines the Processor that will be executed.</param>
		/// <returns>The same <see cref="IServiceCollection"/> passed in for chaining.</returns>
		public static IServiceCollection AddDistenkaProcessor(this IServiceCollection services, Config config)
		{
			return services.AddDistenkaProcessor(config, Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Adds the services required to execute the Processor specified in <paramref name="config"/>. The Processor type must be found in the <paramref name="ProcessAssemblies"/>.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="config">The <see cref="Config"/> that determines the Processor that will be executed.</param>
		/// <param name="ProcessAssemblies">The assemblies where the Processor specified in <paramref name="config"/> can be found.</param>
		/// <returns>The same <see cref="IServiceCollection"/> passed in for chaining.</returns>
		public static IServiceCollection AddDistenkaProcessor(this IServiceCollection services, Config config, params Assembly[] ProcessAssemblies)
		{
			(var cache, _) = services.AddProcessCache(ProcessAssemblies);

			services.AddRunAction(cache, config);

			return services;
		}

		/// <summary>
		/// Adds the services required to perform the action specified in <paramref name="args"/> given the Processes found in the calling assembly.
		/// </summary>
		/// <param name="args">The command line arguments that determine what action the <see cref="ProcessorHost"/> will perform.</param>
		/// <param name="services">The service collection being modified.</param>
		/// <returns>The same <see cref="IServiceCollection"/> passed in for chaining.</returns>
		public static IServiceCollection AddDistenkaProcessor(this IServiceCollection services, string[] args)
		{
			return services.AddDistenkaProcessor(args, Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Adds the services required to perform the action specified in <paramref name="args"/> given the Processes found in the <paramref name="ProcessAssemblies"/>.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="args">The command line arguments that determine what action the <see cref="ProcessorHost"/> will perform.</param>
		/// <param name="ProcessAssemblies">The assemblies where Processes can be found.</param>
		/// <returns>The same <see cref="IServiceCollection"/> passed in for chaining.</returns>
		public static IServiceCollection AddDistenkaProcessor(this IServiceCollection services, string[] args, params Assembly[] ProcessAssemblies)
		{
			(var cache, var reader) = services.AddProcessCache(ProcessAssemblies);

			GetCommandLineBuilder(services, cache, reader)
			.UseMiddleware(async (context, next) =>
			{
				var result = context.ParseResult.RootCommandResult.Children.FirstOrDefault() as CommandResult;

				if (result?.Command.Name == ListCommand || result?.Command.Name == GetCommand || result?.Command.Name == RunCommand ||  context.ParseResult.UnmatchedTokens.Count > 0)
				{
					await next(context);
				}
				else if (result != null) // Processor command
				{
					var config = cache.GetDefaultConfig(result.Command.Name);

					ApplyCommandLineOverrides(config, result.Children);

					services.AddRunAction(cache, config);
				}
				else
				{
					await next(context);
				}
			})
			.UseParseErrorReporting()
			.Build()
			.Invoke(args);

			return services;
		}
		
		/// <summary>
		/// Discovers and caches Processor types from the <paramref name="ProcessAssemblies"/>, creating a singleton service <see cref="ProcessCache"/>.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="ProcessAssemblies">The assemblies where Processes can be found.</param>
		/// <returns>A <see cref="ConfigReader"/> that can read JSON configs for the Processor types in the <see cref="ProcessCache"/>.</returns>
		static (ProcessCache, ConfigReader) AddProcessCache(this IServiceCollection services, IEnumerable<Assembly> ProcessAssemblies)
		{
			// need to use this stuff now; create it then register it
			var cache = new ProcessCache(ProcessAssemblies);
			var cfgReader = new ConfigReader(cache);

			services.AddSingleton(cache);
			services.AddSingleton(cfgReader);

			return (cache, cfgReader);
		}

		static CommandLineBuilder GetCommandLineBuilder(IServiceCollection services, ProcessCache cache, ConfigReader reader)
		{
			var root = new RootCommand();

			var list = new Command(ListCommand, "Lists the Processes available to run.")
			{
				new Option<bool>(new[] { "--verbose", "-v" }, "Optional. Changes the config serialization to include all properties."),
				new Option<bool>(new[] { "--json", "-j" }, "Optional. Changes the output format to JSON.")
			};
			list.Handler = CommandHandler.Create<bool, bool>((verbose, json) => services.AddListAction(verbose, json));
			root.AddCommand(list);

			var get = new Command(GetCommand, "Writes the default config for the specified type to the file path.")
			{
				new Argument<string>("type", "The type of Processor to get the default config for."),
				new Argument<string>("filePath", () => "", "Optional. The file path to write the config to."),
				new Option<bool>(new[] { "--verbose", "-v" }, "Optional. Changes the config serialization to include all properties.")
			};
			get.Handler = CommandHandler.Create<string, string, bool>((type, filePath, verbose) => services.AddGetAction(type, filePath, verbose));
			root.AddCommand(get);

			var run = new Command(RunCommand, "Runs a Processor from a JSON config file.")
			{
				new Argument<FileInfo>(ConfigPathArgument, "The JSON serialized config for the Processor to run.").ExistingOnly(),
				new Argument<FileInfo>("resultsPath", () => null, "Optional. The file path to write the results of the Processor to."),
				new Option<bool>(new[] { "--debug", "-d" }, "Optional. Prompts the user to attach a debugger when the Processor starts."),
				new Option<bool>(new[] { "--silent", "-s" }, "Optional. Silences console output.")
			};
			run.Handler = CommandHandler.Create<FileInfo, FileInfo, bool, bool>((configPath, resultsPath, debug, silent) => services.AddRunAction(cache, reader, configPath, resultsPath, debug, silent));
			root.AddCommand(run);

			// Add a command for each Processor, with each config path as an option
			foreach (var Process in cache.Processes)
			{
				var ProcessCmd = new Command(Process.ProcessType.Name.ToLowerInvariant(), $"Runs the Processor {Process.ProcessType.FullName}.");

				ProcessCmd.AddAlias(Process.ProcessType.Name);

				AddConfigOptions(ProcessCmd, Process.ConfigType, null);

				root.AddCommand(ProcessCmd);
			}

			void AddConfigOptions(Command command, Type type, string prefix)
			{
				foreach (var prop in type.GetProperties())
				{
					var name = prefix == null ? $"--{prop.Name}" : $"{prefix}.{prop.Name}";

					// The Processor type comes from the command and the package/version cannot be set (it's whatever's executing)
					if (name == "--Processor")
						continue;

					bool isArray = prop.PropertyType.IsArray;
					Type propType = isArray ? prop.PropertyType.GetElementType() : prop.PropertyType;

					if (propType.IsValueType || propType == typeof(string))
					{
						if (prop.CanWrite)
						{
							var desc = prop.GetCustomAttributes<DescriptionAttribute>().FirstOrDefault()?.Description;

							var option = new Option(name.ToLowerInvariant(), desc)
							{
								Argument = new Argument(propType.Name)
								{
									Arity = (propType == typeof(bool), isArray) switch
									{
										(true, false) => ArgumentArity.ZeroOrOne,
										(_, true) => ArgumentArity.ZeroOrMore,
										(_, _) => ArgumentArity.ExactlyOne
									},
									ArgumentType = prop.PropertyType
								}
							};
							option.AddAlias(name);
							command.AddOption(option);
						}
					}
					else
					{
						if (!prop.PropertyType.IsArray)
							AddConfigOptions(command, prop.PropertyType, name);
					}
				}
			}

			return new CommandLineBuilder(root);
		}

		/// <summary>
		/// Applies command line config overrides to <paramref name="config"/>.
		/// </summary>
		/// <param name="config">The <see cref="Config"/> to override.</param>
		/// <param name="symbols">A list of overrides to apply to the config.</param>
		static void ApplyCommandLineOverrides(Config config, SymbolResultSet symbols)
		{
			foreach (var symbol in symbols)
			{
				var optr = symbol as OptionResult;

				var path = optr.Option.Name;
				var argr = optr.Children.FirstOrDefault() as ArgumentResult;
				var prop = path.Split('.');

				object cfg = null;
				PropertyInfo pi = null;
				var type = config.GetType();

				for (int i = 0; i < prop.Length; i++)
				{
					cfg = pi?.GetValue(cfg) ?? config;
					pi = type.GetProperty(prop[i]);

					if (pi == null)
						pi = type.GetProperties().SingleOrDefault(p => p.Name.Equals(prop[i], StringComparison.InvariantCultureIgnoreCase));

					if (pi == null)
						throw new ArgumentException($"Could not find '{prop[i]}' in the config path '{path}'");

					type = pi.PropertyType;
				}

				pi.SetValue(cfg, argr.GetValueOrDefault());
			}
		}

		static void AddListAction(this IServiceCollection services, bool verbose, bool json)
		{
			services.AddTransient<JsonSchema>();

			services.AddTransient<IHostedService>(s => new ListAction(
				verbose,
				json,
				s.GetRequiredService<ProcessCache>(),
				s.GetRequiredService<JsonSchema>(),
                s.GetRequiredService<IHostApplicationLifetime>()
            ));
		}

		static void AddGetAction(this IServiceCollection services, string type, string filePath, bool verbose)
		{
			services.AddTransient<IHostedService>(s => new GetAction(
				verbose,
				type,
				string.IsNullOrWhiteSpace(filePath) ? null : filePath,
				s.GetRequiredService<ProcessCache>(),
				s.GetRequiredService<IHostApplicationLifetime>()
			));
		}

		/// <summary>
		/// Adds a transient <see cref="RunAction"/> to the <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="reader">A <see cref="ConfigReader"/> that can read the JSON config file specified in <paramref name="configPath"/>.</param>
		/// <param name="configPath">The location of the JSON config to run.</param>
		/// <param name="debug">Indicates whether to attach a debugger.</param>
		/// <param name="silent">Indicates whether to output results to the console.</param>
		static void AddRunAction(this IServiceCollection services, ProcessCache cache, ConfigReader reader, FileInfo configPath, FileInfo resultsPath,bool debug, bool silent)
		{
			if (debug)
				services.AddSingleton(new Debug() { AttachDebugger = true });

			var config = reader.FromFile(configPath.FullName);
			config.__filePath = configPath.FullName;

			if (!string.IsNullOrWhiteSpace(configPath.Directory.FullName))
				Directory.SetCurrentDirectory(configPath.Directory.FullName);

			if (resultsPath != null)
			{
				config.Execution.ResultsFilePath = resultsPath.FullName;
				config.Execution.ResultsToFile = true;
			}

			if (silent)
				config.Execution.ResultsToConsole = false;

			services.AddRunAction(cache, config);
		}

		/// <summary>
		/// Adds a transient <see cref="RunAction"/> to the <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="config">The <see cref="Config"/> for the Processor.</param>
		static void AddRunAction(this IServiceCollection services, ProcessCache cache, Config config)
		{
			services.AddProcess(cache, config);

			services.AddSingleton<TextWriter>(s =>
			{
				if (!config.Execution.ResultsToFile)
					return new VoidTextWriter();

				if (string.IsNullOrWhiteSpace(config.Execution.ResultsFilePath))
					config.Execution.ResultsFilePath = "results.json";

				return new StreamWriter(File.Open(config.Execution.ResultsFilePath, FileMode.Create));
			});

			services.AddSingleton<TextWriter>(s =>
			{
				if (!config.Execution.ResultsToConsole)
					return new VoidTextWriter();

				return Console.Out;
			});

			services.AddScoped<Execution>(s =>
			{
				var cache = s.GetRequiredService<ProcessCache>();

				var info = cache.Get(config.Process.Type);

				var Process = (IProcessor)s.GetRequiredService(info.ProcessType);
				return Process.GetExecution(s.GetRequiredService<IServiceProvider>());
			});

			services.AddTransient<IHostedService>(s => new RunAction(
				s.GetRequiredService<Config>(),
                s.GetRequiredService<IHostApplicationLifetime>(),
                s,
				s.GetRequiredService<ILogger<RunAction>>(),
				s.GetService<Debug>()
			));
		}

		/// <summary>
		/// Adds the <paramref name="config"/> to the <see cref="IServiceCollection"/> as a singleton service using the class itself, each base class, and each interface.
		/// </summary>
		/// <param name="services">The service collection being modified.</param>
		/// <param name="config">The <see cref="Config"/> to add to the service collection.</param>
		/// <returns>The same <see cref="IServiceCollection"/> passed in for chaining.</returns>
		public static IServiceCollection AddProcess(this IServiceCollection services, ProcessCache cache, Config config)
		{
            var info = cache.Get(config.Process.Type);

            services.AddScoped(info.ProcessType);

            var type = config.GetType();

			foreach (var intf in type.GetInterfaces())
				services.AddSingleton(intf, config);

			do
			{
				services.AddSingleton(type, config);
				type = type.BaseType;
			} while (type != null);

			return services;
		}

		/// <summary>
		/// Adds a transient service of the type specified in <paramref name="serviceType"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="serviceType">The type of the service to register.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Transient"/>
		public static IServiceCollection AddTransient(this IServiceCollection services, Type serviceType, Func<IServiceProvider, Config, object> implementationFactory)
		{
			return services.AddTransient(serviceType, s => implementationFactory(s, s.GetRequiredService<Config>()));
		}

		/// <summary>
		/// Adds a transient service of the type specified in <typeparamref name="TService"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Transient"/>
		public static IServiceCollection AddTransient<TService, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TService> implementationFactory)
			where TService : class
			where TConfig : Config
		{
			return services.AddTransient<TService>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}

		/// <summary>
		/// Adds a transient service of the type specified in <typeparamref name="TService"/> with an
		/// implementation type specified in <typeparamref name="TImplementation"/> and a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Transient"/>
		public static IServiceCollection AddTransient<TService, TImplementation, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService
			where TConfig : Config
		{
			return services.AddTransient<TService, TImplementation>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}

		/// <summary>
		/// Adds a scoped service of the type specified in <paramref name="serviceType"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="serviceType">The type of the service to register.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Scoped"/>
		public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Func<IServiceProvider, Config, object> implementationFactory)
		{
			return services.AddScoped(serviceType, s => implementationFactory(s, s.GetRequiredService<Config>()));
		}

		/// <summary>
		/// Adds a scoped service of the type specified in <typeparamref name="TService"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Scoped"/>
		public static IServiceCollection AddScoped<TService, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TService> implementationFactory)
			where TService : class
			where TConfig : Config
		{
			return services.AddScoped<TService>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}

		/// <summary>
		/// Adds a scoped service of the type specified in <typeparamref name="TService"/> with an
		/// implementation type specified in <typeparamref name="TImplementation"/> and a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Scoped"/>
		public static IServiceCollection AddScoped<TService, TImplementation, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService
			where TConfig : Config
		{
			return services.AddScoped<TService, TImplementation>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}

		/// <summary>
		/// Adds a singleton service of the type specified in <paramref name="serviceType"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="serviceType">The type of the service to register.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Singleton"/>
		public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Func<IServiceProvider, Config, object> implementationFactory)
		{
			return services.AddSingleton(serviceType, s => implementationFactory(s, s.GetRequiredService<Config>()));
		}

		/// <summary>
		/// Adds a singleton service of the type specified in <typeparamref name="TService"/> with a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Singleton"/>
		public static IServiceCollection AddSingleton<TService, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TService> implementationFactory)
			where TService : class
			where TConfig : Config
		{
			return services.AddSingleton<TService>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}

		/// <summary>
		/// Adds a singleton service of the type specified in <typeparamref name="TService"/> with an
		/// implementation type specified in <typeparamref name="TImplementation"/> and a
		/// factory specified in <paramref name="implementationFactory"/> to the
		/// specified <see cref="IServiceCollection"/>.
		/// </summary>
		/// <typeparam name="TService">The type of the service to add.</typeparam>
		/// <typeparam name="TImplementation">The type of the implementation to use.</typeparam>
		/// <typeparam name="TConfig">The type of <see cref="Config" /> that the Processor uses.</typeparam>
		/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
		/// <param name="implementationFactory">The factory that creates the service that takes the Processor's config as a parameter.</param>
		/// <returns>A reference to this instance after the operation has completed.</returns>
		/// <seealso cref="ServiceLifetime.Singleton"/>
		public static IServiceCollection AddSingleton<TService, TImplementation, TConfig>(this IServiceCollection services, Func<IServiceProvider, TConfig, TImplementation> implementationFactory)
			where TService : class
			where TImplementation : class, TService
			where TConfig : Config
		{
			return services.AddSingleton<TService, TImplementation>(s => implementationFactory(s, s.GetRequiredService<TConfig>()));
		}
	}
}
