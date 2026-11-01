using System;

namespace Distenka.Hosting
{
	/// <summary>
	/// An exception thrown when the Processor type cannot be resolved.
	/// </summary>
	public class TypeNotFoundException : Exception
	{
		/// <summary>
		/// Initializes a new <see cref="TypeNotFoundException"/>.
		/// </summary>
		/// <param name="typeName"></param>
		public TypeNotFoundException(string typeName)
			: base($"Type {typeName} not found.")
		{
			TypeName = typeName;
		}

		/// <summary>
		/// Initializes a new <see cref="TypeNotFoundException"/> with a specific message.
		/// </summary>
		protected internal TypeNotFoundException(string typeName, string message)
			: base(message)
		{
			TypeName = typeName;
		}

		/// <summary>The process name that could not be resolved.</summary>
		public string TypeName { get; }
	}

	/// <summary>
	/// Thrown when a process name matches several processes at the same resolution step (ADR-002 §2.2 rule 3).
	/// </summary>
	public class AmbiguousProcessException : TypeNotFoundException
	{
		/// <summary>Initializes a new <see cref="AmbiguousProcessException"/>.</summary>
		public AmbiguousProcessException(string typeName, string message)
			: base(typeName, message) { }
	}
}
