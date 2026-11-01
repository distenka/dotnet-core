using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Distenka.Hosting
{
	/// <summary>
	/// A cache of <see cref="ProcessInfo">ProcessInfos</see> for the Processor types found in the assemblies provided.
	/// </summary>
	public class ProcessCache
	{
		/// <summary>
		/// Gets the Processes found in the assemblies provided.
		/// </summary>
		public IEnumerable<ProcessInfo> Processes { get; }

		/// <summary>
		/// Initializes a new <see cref="ProcessCache"/>.
		/// </summary>
		/// <param name="ProcessAssemblies">The assemblies to search for Processes.</param>
		public ProcessCache(IEnumerable<Assembly> ProcessAssemblies)
		{
			Processes = FindProcessTypes(ProcessAssemblies);
		}

		/// <summary>Builds a cache from explicit processor types (tests; DCS-043).</summary>
		internal ProcessCache(IEnumerable<Type> processTypes)
		{
			Processes = processTypes.Select(t => new ProcessInfo(t)).ToList();
		}

		IEnumerable<ProcessInfo> FindProcessTypes(IEnumerable<Assembly> ProcessAssemblies)
		{
			var result = new List<ProcessInfo>();

			var processTypes = ProcessAssemblies
				.SelectMany(a => a.GetTypes())
				.Where(t => typeof(IProcessor).IsAssignableFrom(t) && !t.IsInterface);

			foreach (var type in processTypes)
				result.Add(new ProcessInfo(type));

			return result;
		}

		/// <summary>
		/// Gets the <see cref="ProcessInfo"/> for the Processor type specified.
		/// </summary>
		/// <param name="type">The type to get the <see cref="ProcessInfo"/> for.</param>
		/// <returns>A <see cref="ProcessInfo"/> or null if the type cannot be found.</returns>
		public ProcessInfo Get(Type type)
		{
			return Processes.FirstOrDefault(pi => pi.ProcessType == type);
		}

		/// <summary>
		/// Gets the <see cref="ProcessInfo"/> for the Processor type specified.
		/// </summary>
		/// <param name="ProcessType">The type to get the <see cref="ProcessInfo"/> for.</param>
		/// <returns>A <see cref="ProcessInfo"/> or null if the type cannot be found.</returns>
		/// <remarks>
		/// Resolution follows ADR-002 §2.2 rule 3 over the valid processes only: exact <see cref="Type.FullName"/>, then exact
		/// short name, then a case-insensitive match of either. Several matches at one step throw
		/// <see cref="AmbiguousProcessException"/>; no match throws <see cref="TypeNotFoundException"/> listing the canonical names.
		/// </remarks>
		public ProcessInfo Get(string ProcessType)
		{
			return ResolveProcessInfo(ProcessType);
		}

		/// <summary>
		/// Gets the default <see cref="Config"/> for the Processor type specified.
		/// </summary>
		/// <param name="ProcessType">The type to get the default config for.</param>
		/// <remarks>
		/// An exact case match is attempted first for either the type <see cref="MemberInfo.Name"/>
		/// or <see cref="Type.FullName"/> of the Processor type. If no matches are found a case insensitive 
		/// match is attempted. If zero or more than one Processor type matches, a <see cref="TypeNotFoundException"/>
		/// will be thrown.
		/// </remarks>
		public Config GetDefaultConfig(string ProcessType)
		{
			return GetDefaultConfig(ResolveProcessInfo(ProcessType));
		}

		/// <summary>
		/// Gets the default <see cref="Config"/> for the <paramref name="ProcessInfo"/>.
		/// </summary>
		/// <param name="ProcessInfo">The <see cref="ProcessInfo"/> to get the default config for.</param>
		/// <returns>The default config for the Processor.</returns>
		public Config GetDefaultConfig(ProcessInfo ProcessInfo)
		{
			var config = (Config)Activator.CreateInstance(ProcessInfo.ConfigType);

			config.Process.Type = ProcessInfo.ProcessType.FullName;
			config.Process.Package = ProcessInfo.ProcessType.Assembly.GetName().Name;
			config.Process.Version = ProcessInfo.ProcessType.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

			return config;
		}

		/// <summary>The valid (runnable) processes, ordered by canonical name (ordinal) — ADR-002 §2.3 rule 6.</summary>
		public IReadOnlyList<ProcessInfo> ValidProcesses =>
			Processes.Where(p => p.IsValid).OrderBy(p => p.ProcessType.FullName, StringComparer.Ordinal).ToList();

		ProcessInfo ResolveProcessInfo(string type)
		{
			var valid = ValidProcesses;
			var index = ProcessResolver.Resolve(type, valid.Select(p => (p.ProcessType.FullName, p.ProcessType.Name)).ToList());
			return valid[index];
		}
	}

	/// <summary>
	/// Resolves a process name against a process set (ADR-002 §2.2 rule 3): exact canonical name (<c>FullName</c>), then
	/// exact short name, then case-insensitive canonical or short name. A step with exactly one match wins; a step with
	/// several is an ambiguity error; no match after the last step is an unknown-process error listing the canonical names.
	/// </summary>
	internal static class ProcessResolver
	{
		/// <summary>Returns the index of the resolved process in <paramref name="processes"/>.</summary>
		/// <exception cref="AmbiguousProcessException">Several processes match at one step.</exception>
		/// <exception cref="TypeNotFoundException">No process matches.</exception>
		public static int Resolve(string requested, IReadOnlyList<(string FullName, string Name)> processes)
		{
			var steps = new Func<(string FullName, string Name), bool>[]
			{
				p => string.Equals(p.FullName, requested, StringComparison.Ordinal),
				p => string.Equals(p.Name, requested, StringComparison.Ordinal),
				p => string.Equals(p.FullName, requested, StringComparison.OrdinalIgnoreCase) ||
					 string.Equals(p.Name, requested, StringComparison.OrdinalIgnoreCase)
			};

			if (!string.IsNullOrWhiteSpace(requested))
			{
				foreach (var step in steps)
				{
					var matches = Enumerable.Range(0, processes.Count).Where(i => step(processes[i])).ToList();

					if (matches.Count == 1)
						return matches[0];

					if (matches.Count > 1)
						throw new AmbiguousProcessException(requested,
							$"process '{requested}' is ambiguous; it matches {string.Join(", ", matches.Select(i => processes[i].FullName))}");
				}
			}

			throw new TypeNotFoundException(requested,
				$"process '{requested}' is not in this package; available: {Available(processes)}");
		}

		internal static string Available(IEnumerable<(string FullName, string Name)> processes)
		{
			var names = processes.Select(p => p.FullName).ToList();
			return names.Count == 0 ? "(none)" : string.Join(", ", names);
		}
	}
}
