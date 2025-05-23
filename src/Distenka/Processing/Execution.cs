﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Distenka.Processing
{
	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	public class Execution<TConfig> : ExecutionBase<TConfig, string>
		where TConfig : Config
	{
		readonly Processor<TConfig> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig> processor, IServiceProvider provider)
			: base(new SingleItemSource(), provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, string item)
		{
			return await _processor.ProcessAsync();
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	public class Execution<TConfig, TItem> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			return await _processor.ProcessAsync(item);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();

			return await _processor.ProcessAsync(item, arg1);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();

			return await _processor.ProcessAsync(item, arg1, arg2);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T12">The twelfth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();
			var arg12 = scope.ServiceProvider.GetRequiredService<T12>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T12">The twelfth dependency type of the Processor.</typeparam>
	/// <typeparam name="T13">The thirteenth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();
			var arg12 = scope.ServiceProvider.GetRequiredService<T12>();
			var arg13 = scope.ServiceProvider.GetRequiredService<T13>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T12">The twelfth dependency type of the Processor.</typeparam>
	/// <typeparam name="T13">The thirteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T14">The fourteenth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();
			var arg12 = scope.ServiceProvider.GetRequiredService<T12>();
			var arg13 = scope.ServiceProvider.GetRequiredService<T13>();
			var arg14 = scope.ServiceProvider.GetRequiredService<T14>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T12">The twelfth dependency type of the Processor.</typeparam>
	/// <typeparam name="T13">The thirteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T14">The fourteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T15">The fifteenth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> _processor;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => Processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();
			var arg12 = scope.ServiceProvider.GetRequiredService<T12>();
			var arg13 = scope.ServiceProvider.GetRequiredService<T13>();
			var arg14 = scope.ServiceProvider.GetRequiredService<T14>();
			var arg15 = scope.ServiceProvider.GetRequiredService<T15>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
		}
	}

	/// <summary>
	/// The <see cref="Execution"/> implementation for <see cref="Processoror{TConfig,TItem,T1,T2,T3,T4,T5,T6,T7,T8,T9,T10,T11,T12,T13,T14,T15,T16}"/>.
	/// </summary>
	/// <typeparam name="TConfig">The <see cref="Config"/> type of the Processor.</typeparam>
	/// <typeparam name="TItem">The item type of the Processor.</typeparam>
	/// <typeparam name="T1">The first dependency type of the Processor.</typeparam>
	/// <typeparam name="T2">The second dependency type of the Processor.</typeparam>
	/// <typeparam name="T3">The third dependency type of the Processor.</typeparam>
	/// <typeparam name="T4">The fourth dependency type of the Processor.</typeparam>
	/// <typeparam name="T5">The fifth dependency type of the Processor.</typeparam>
	/// <typeparam name="T6">The sixth dependency type of the Processor.</typeparam>
	/// <typeparam name="T7">The seventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T8">The eighth dependency type of the Processor.</typeparam>
	/// <typeparam name="T9">The ninth dependency type of the Processor.</typeparam>
	/// <typeparam name="T10">The tenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T11">The eleventh dependency type of the Processor.</typeparam>
	/// <typeparam name="T12">The twelfth dependency type of the Processor.</typeparam>
	/// <typeparam name="T13">The thirteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T14">The fourteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T15">The fifteenth dependency type of the Processor.</typeparam>
	/// <typeparam name="T16">The sixteenth dependency type of the Processor.</typeparam>
	public class Execution<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> : ExecutionBase<TConfig, TItem>
		where TConfig : Config
	{
		readonly Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> _processor;
		
		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public override IProcessor Processor => _processor;

		/// <summary>
		/// Initializes a new <see cref="Execution{TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16}"/>.
		/// </summary>
		/// <param name="processor">The Processor to execute.</param>
		/// <param name="source">The <see cref="IItemSource{TItem}"/> used to get the items to _processor.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> used to get ProcessAsync arguments.</param>
		public Execution(Processor<TConfig, TItem, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16> processor, IItemSource<TItem> source, IServiceProvider provider)
			: base(source, provider)
		{
			this._processor = processor ?? throw new ArgumentNullException(nameof(processor));
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to this <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected async override Task<Result> CallProcess(IServiceScope scope, TItem item)
		{
			var arg1 = scope.ServiceProvider.GetRequiredService<T1>();
			var arg2 = scope.ServiceProvider.GetRequiredService<T2>();
			var arg3 = scope.ServiceProvider.GetRequiredService<T3>();
			var arg4 = scope.ServiceProvider.GetRequiredService<T4>();
			var arg5 = scope.ServiceProvider.GetRequiredService<T5>();
			var arg6 = scope.ServiceProvider.GetRequiredService<T6>();
			var arg7 = scope.ServiceProvider.GetRequiredService<T7>();
			var arg8 = scope.ServiceProvider.GetRequiredService<T8>();
			var arg9 = scope.ServiceProvider.GetRequiredService<T9>();
			var arg10 = scope.ServiceProvider.GetRequiredService<T10>();
			var arg11 = scope.ServiceProvider.GetRequiredService<T11>();
			var arg12 = scope.ServiceProvider.GetRequiredService<T12>();
			var arg13 = scope.ServiceProvider.GetRequiredService<T13>();
			var arg14 = scope.ServiceProvider.GetRequiredService<T14>();
			var arg15 = scope.ServiceProvider.GetRequiredService<T15>();
			var arg16 = scope.ServiceProvider.GetRequiredService<T16>();

			return await _processor.ProcessAsync(item, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
		}
	}
}
