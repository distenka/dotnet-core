using System;

namespace Distenka.Hosting
{
	/// <summary>
	/// Error reasons that <see cref="ProcessInfo"/> may encounter when loading a Processor.
	/// </summary>
	[Flags]
	public enum ProcessLoadErrors
	{
		/// <summary>
		/// No errors loading the Processor.
		/// </summary>
		None = 0,

		/// <summary>
		/// The Processor type is an interface and cannot be instantiated.
		/// </summary>
		IsInterface = 1,

		/// <summary>
		/// The Processor type is an abstract class and cannot be instantiated.
		/// </summary>
		IsAbstract = 2,

		/// <summary>
		/// The Processor type is a generic type definition and cannot be instantiated.
		/// </summary>
		IsGenericTypeDefinition = 4
	}
}
