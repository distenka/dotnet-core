using Distenka.Processing;
using System;
using System.Threading.Tasks;

namespace Distenka
{
	/// <summary>
	/// A single-action asynchronous Processor.
	/// </summary>
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	public abstract class Processor<TConfig> : ProcessorBase<TConfig>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig>(this, provider);

		/// <summary>
		/// Performs the processing for the Processor.
		/// </summary>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync();
	}

	/// <summary>
	/// A Processor that provides a list of items and an action to perform on each item.
	/// </summary>
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	public abstract class Processor<TConfig, TItem> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item);
	}

	/// <summary>
	/// A Processor with one dependency that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1);
	}

	/// <summary>
	/// A Processor with two dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2);
	}

	/// <summary>
	/// A Processor with three dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3);
	}

	/// <summary>
	/// A Processor with four dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
	}

	/// <summary>
	/// A Processor with five dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
	}

	/// <summary>
	/// A Processor with six dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
	}

	/// <summary>
	/// A Processor with seven dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
	}

	/// <summary>
	/// A Processor with eight dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
	}

	/// <summary>
	/// A Processor with nine dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
	}

	/// <summary>
	/// A Processor with ten dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
	}

	/// <summary>
	/// A Processor with eleven dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
	}

	/// <summary>
	/// A Processor with twelve dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T12">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <param name="arg12">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
	}

	/// <summary>
	/// A Processor with thirteen dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T12">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T13">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <param name="arg12">A dependency required to process the item.</param>
		/// <param name="arg13">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
	}

	/// <summary>
	/// A Processor with fourteen dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T12">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T13">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T14">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <param name="arg12">A dependency required to process the item.</param>
		/// <param name="arg13">A dependency required to process the item.</param>
		/// <param name="arg14">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);
	}

	/// <summary>
	/// A Processor with fifteen dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T12">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T13">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T14">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T15">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> : ProcessorBase<TConfig, TItem>
		where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <param name="arg12">A dependency required to process the item.</param>
		/// <param name="arg13">A dependency required to process the item.</param>
		/// <param name="arg14">A dependency required to process the item.</param>
		/// <param name="arg15">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15);
	}

	/// <summary>
	/// A Processor with sixteen dependencies that provides a list of items and an action to perform on each item.
	/// </summary>	
	/// <typeparam name="TConfig">The configuration for the Processor.</typeparam>
	/// <typeparam name="TItem">The data required to process each item.</typeparam>
	/// <typeparam name="T1">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T2">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T3">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T4">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T5">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T6">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T7">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T8">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T9">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T10">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T11">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T12">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T13">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T14">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T15">A dependency passed into the ProcessAsync method.</typeparam>
	/// <typeparam name="T16">A dependency passed into the ProcessAsync method.</typeparam>
	public abstract class Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> : ProcessorBase<TConfig, TItem>
	where TConfig : Config
	{
		/// <summary>
		/// Initializes a new Processor.
		/// </summary>
		/// <param name="config">The config for the Processor.</param>
		protected Processor(TConfig config)
			: base(config) { }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation to execute this Processor. You should not override this method.
		/// </summary>
		protected internal override Execution GetExecution(IServiceProvider provider) => new Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(this, this, provider);

		/// <summary>
		/// Processes the specified <paramref name="item"/>.
		/// </summary>
		/// <param name="item">The item to process.</param>
		/// <param name="arg1">A dependency required to process the item.</param>
		/// <param name="arg2">A dependency required to process the item.</param>
		/// <param name="arg3">A dependency required to process the item.</param>
		/// <param name="arg4">A dependency required to process the item.</param>
		/// <param name="arg5">A dependency required to process the item.</param>
		/// <param name="arg6">A dependency required to process the item.</param>
		/// <param name="arg7">A dependency required to process the item.</param>
		/// <param name="arg8">A dependency required to process the item.</param>
		/// <param name="arg9">A dependency required to process the item.</param>
		/// <param name="arg10">A dependency required to process the item.</param>
		/// <param name="arg11">A dependency required to process the item.</param>
		/// <param name="arg12">A dependency required to process the item.</param>
		/// <param name="arg13">A dependency required to process the item.</param>
		/// <param name="arg14">A dependency required to process the item.</param>
		/// <param name="arg15">A dependency required to process the item.</param>
		/// <param name="arg16">A dependency required to process the item.</param>
		/// <returns>A <see cref="Result"/> indicating success or failure.</returns>
		public abstract Task<Result> ProcessAsync(TItem item, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16);
	}
}
