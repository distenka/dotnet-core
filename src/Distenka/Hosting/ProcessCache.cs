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
		/// An exact case match is attempted first for either the type <see cref="MemberInfo.Name"/>
		/// or <see cref="Type.FullName"/>. If no matches are found a case insensitive match is attempted.
		/// If zero or more than one Processor type matches, a <see cref="TypeNotFoundException"/> will be thrown.
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

		ProcessInfo ResolveProcessInfo(string type)
		{
			var types = Processes.Where(i => i.ProcessType.Name == type || i.ProcessType.FullName == type);

			if (types.Count() == 0)
				types = Processes.Where(i => i.ProcessType.Name.ToLower() == type.ToLower() || i.ProcessType.FullName.ToLower() == type.ToLower());

			if (types.Count() != 1)
				throw new TypeNotFoundException(type);

			return types.Single();
		}
	}
}
