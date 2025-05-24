using System;

namespace Distenka.Processing
{
	/// <summary>
	/// An exception used internally in <see cref="Execution"/>.
	/// </summary>
	public class ExecutionException : Exception
	{
		/// <summary>
		/// Initializes a new <see cref="ExecutionException"/>..
		/// </summary>
		/// <param name="pointOfFailure">The location where a failure occured.</param>
		/// <param name="innerException">The underlying cause of this exception.</param>
		public ExecutionException(ProcessorMethod pointOfFailure, Exception innerException)
			: base($"Processor failed at {pointOfFailure.ToString()}", innerException) { }
	}
}
