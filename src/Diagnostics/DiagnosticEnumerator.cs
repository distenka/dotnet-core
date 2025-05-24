using Distenka.Processing;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Distenka.Diagnostics
{
	public class DiagnosticEnumerator<T> : IEnumerator<T>
	{
		private MethodResponse moveNext;
		private MethodResponse getCurrent;

		public DiagnosticEnumerator(MethodResponse moveNext, MethodResponse getCurrent)
		{
			this.moveNext = moveNext;
			this.getCurrent = getCurrent;
		}

		public T Current => GetCurrent();

		object IEnumerator.Current
		{
			get
			{
				return GetCurrent();
			}
		}

		private T GetCurrent()
		{
			if (getCurrent == MethodResponse.ThrowException)
				throw new DiagnosticProcessException(ProcessorMethod.EnumeratorCurrent, "Exception thrown because Config.ThrowExceptionInEnumeratorCurrent is true.");
			else
				return default(T);
		}

		public void Dispose() { }

		public bool MoveNext()
		{
			if (moveNext == MethodResponse.ThrowException)
				throw new DiagnosticProcessException(ProcessorMethod.EnumeratorMoveNext, "Exception thrown because Config.ThrowExceptionInEnumeratorMoveNext is true.");
			else
				return true;
		}

		public void Reset()
		{
			throw new NotImplementedException();
		}
	}
}
