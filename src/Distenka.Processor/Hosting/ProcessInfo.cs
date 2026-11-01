using System;
using System.Collections.Generic;

namespace Distenka.Hosting
{
	/// <summary>
	/// Stores the information required to use and run Processes.
	/// </summary>
	public class ProcessInfo
	{
		/// <summary>
		/// The <see cref="Type"/> of the Processor.
		/// </summary>
		public Type ProcessType { get; private set; }

		/// <summary>
		/// The <see cref="Type"/> of the item the Processor works with.
		/// </summary>
		public Type ItemType { get; private set; }

		/// <summary>
		/// The <see cref="Type"/> of <see cref="Config"/> the Processor uses.
		/// </summary>
		public Type ConfigType { get; private set; }

		/// <summary>
		/// The <see cref="Type">Types</see> of the parameters for ProcessAsync.
		/// </summary>
		public Type[] Dependencies { get; private set; }

		/// <summary>
		/// The reasons why the <see cref="ProcessType"/> cannot be used as a Processor.
		/// </summary>
		public ProcessLoadErrors Errors { get; private set; }

		/// <summary>
		/// Indicates whether there are any errors with the <see cref="ProcessType"/>.
		/// </summary>
		public bool IsValid => Errors == ProcessLoadErrors.None;

		/// <summary>
		/// Initializes a new <see cref="ProcessInfo"/>.
		/// </summary>
		/// <param name="ProcessType">The <see cref="Type"/> of the Processor.</param>
		public ProcessInfo(Type ProcessType)
		{
			this.ProcessType = ProcessType ?? throw new ArgumentNullException(nameof(ProcessType));

			if (!typeof(IProcessor).IsAssignableFrom(ProcessType))
				throw new ArgumentException($"{ProcessType.FullName} does not implement {typeof(IProcessor).FullName}");

			Errors |= ProcessType.IsInterface ? ProcessLoadErrors.IsInterface : ProcessType.IsAbstract ? ProcessLoadErrors.IsAbstract : ProcessLoadErrors.None;
			Errors |= ProcessType.IsGenericTypeDefinition ? ProcessLoadErrors.IsGenericTypeDefinition : ProcessLoadErrors.None;

			FindProcessBaseType(ProcessType);
		}

		private void FindProcessBaseType(Type ProcessType)
		{
			// The types we're looking for are abstract so they will
			// be extended at least one time.
			do
			{
				ProcessType = ProcessType.BaseType;
			}
			while (!IsProcessBaseType(ProcessType));

			ReadGenericTypeArguments(ProcessType);
		}

		private bool IsProcessBaseType(Type type)
		{
			// Looking for the generic types which will have the generic type 
			// parameters specified, so we can't know the full type at compile time.
			return type.Assembly == typeof(ProcessInfo).Assembly && type.IsGenericType && type.Name.StartsWith("Processor");
		}

		private void ReadGenericTypeArguments(Type type)
		{
			// This method relies on the generic type parameters for all
			// process base classes being ordered: Config, Item, Dep1, Dep2...DepN

			this.ConfigType = type.GenericTypeArguments[0];

			if (type.GenericTypeArguments.Length > 1)
				this.ItemType = type.GenericTypeArguments[1];

			var types = new List<Type>();

			for (int i = 2; i < type.GenericTypeArguments.Length; i++)
				types.Add(type.GenericTypeArguments[i]);

			this.Dependencies = types.ToArray();
		}
	}
}
