using System;

namespace Distenka.Diagnostics
{
	[Serializable]
	public class DiagnosticProcessException : Exception
	{
		public ProcessorMethod Location { get; private set; }

		public DiagnosticProcessException(ProcessorMethod location, string message)
			: base(message)
		{
			Location = location;
		}
	}
}
