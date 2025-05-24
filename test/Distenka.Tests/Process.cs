using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Tests
{
	public class SignalConfig : Config
	{
		public bool WaitForSignal { get; set; }
	}

	public class ProcessesingleItem : Processor<Config>
	{
		public ProcessesingleItem() : base(new Config()) { }
		public override Task<Result> ProcessAsync() => Task.FromResult(Result.Success());
	}

	public class Process0 : Processor<Config, int>
	{
		public Process0() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item)
		{
			throw new NotImplementedException();
		}
	}

	public class Process1 : Processor<Config, int, Dep1>
	{
		public Process1() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1)
		{
			throw new NotImplementedException();
		}
	}

    public class Process1WithConstructorDep : Processor<Config, int>
    {
        public Process1WithConstructorDep(IDep1 dep1) : base(new Config()) { }

        public override IAsyncEnumerable<int> GetItemsAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<Result> ProcessAsync(int item)
        {
            throw new NotImplementedException();
        }
    }

    public class Process2 : Processor<Config, int, Dep1, Dep2>
	{
		public Process2() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2)
		{
			throw new NotImplementedException();
		}
	}

    public class Process2WithConstructorDep : Processor<SignalConfig, int>
    {
		readonly AutoResetEvent _signal;

        public Process2WithConstructorDep(SignalConfig config, IDep1 dep1, IDep2 dep2, AutoResetEvent signal)
			: base(config) 
		{
			_signal = signal;
		}

        public override IAsyncEnumerable<int> GetItemsAsync() => new[] { 0 }.ToAsyncEnumerable();

        public override Task<Result> ProcessAsync(int item)
        {
			if (Config.WaitForSignal)
				_signal.WaitOne();

			return Task.FromResult(Result.Success());
        }
    }

    public class Process3 : Processor<Config, int, Dep1, Dep2, Dep3>
	{
		public Process3() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3)
		{
			throw new NotImplementedException();
		}
	}

	public class Process4 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4>
	{
		public Process4() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4)
		{
			throw new NotImplementedException();
		}
	}

	public class Process5 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5>
	{
		public Process5() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5)
		{
			throw new NotImplementedException();
		}
	}

	public class Process6 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6>
	{
		public Process6() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6)
		{
			throw new NotImplementedException();
		}
	}

	public class Process7 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7>
	{
		public Process7() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7)
		{
			throw new NotImplementedException();
		}
	}

	public class Process8 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8>
	{
		public Process8() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8)
		{
			throw new NotImplementedException();
		}
	}

	public class Process9 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9>
	{
		public Process9() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9)
		{
			throw new NotImplementedException();
		}
	}

	public class Process10 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10>
	{
		public Process10() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10)
		{
			throw new NotImplementedException();
		}
	}

	public class Process11 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11>
	{
		public Process11() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11)
		{
			throw new NotImplementedException();
		}
	}

	public class Process12 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11, Dep12>
	{
		public Process12() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11, Dep12 arg12)
		{
			throw new NotImplementedException();
		}
	}

	public class Process13 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11, Dep12, Dep13>
	{
		public Process13() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11, Dep12 arg12, Dep13 arg13)
		{
			throw new NotImplementedException();
		}
	}

	public class Process14 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11, Dep12, Dep13, Dep14>
	{
		public Process14() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11, Dep12 arg12, Dep13 arg13, Dep14 arg14)
		{
			throw new NotImplementedException();
		}
	}

	public class Process15 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11, Dep12, Dep13, Dep14, Dep15>
	{
		public Process15() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11, Dep12 arg12, Dep13 arg13, Dep14 arg14, Dep15 arg15)
		{
			throw new NotImplementedException();
		}
	}

	public class Process16 : Processor<Config, int, Dep1, Dep2, Dep3, Dep4, Dep5, Dep6, Dep7, Dep8, Dep9, Dep10, Dep11, Dep12, Dep13, Dep14, Dep15, Dep16>
	{
		public Process16() : base(new Config()) { }

		public override IAsyncEnumerable<int> GetItemsAsync()
		{
			throw new NotImplementedException();
		}

		public override Task<Result> ProcessAsync(int item, Dep1 arg1, Dep2 arg2, Dep3 arg3, Dep4 arg4, Dep5 arg5, Dep6 arg6, Dep7 arg7, Dep8 arg8, Dep9 arg9, Dep10 arg10, Dep11 arg11, Dep12 arg12, Dep13 arg13, Dep14 arg14, Dep15 arg15, Dep16 arg16)
		{
			throw new NotImplementedException();
		}
	}
}
