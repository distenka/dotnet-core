﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Processing
{
	/// <summary>
	/// An internal base class for <see cref="Execution"/> implementations. This class should not be extended.
	/// </summary>
	/// <typeparam name="TConfig">The Processor's <see cref="Config"/> type.</typeparam>
	/// <typeparam name="TItem">The Processor's item type.</typeparam>
	public abstract class ExecutionBase<TConfig, TItem> : Execution
		where TConfig : Config
	{
		readonly IItemSource<TItem> source;
		readonly IServiceProvider provider;
		readonly ILogger<ExecutionBase<TConfig, TItem>> logger;
		readonly SemaphoreSlim moveNextLock = new SemaphoreSlim(1, 1);
		IAsyncEnumerator<TItem> enumerator;

		/// <summary>
		/// Initializes a new <see cref="ExecutionBase{TConfig, TItem}"/>.
		/// </summary>
		/// <param name="source">The source of items to process.</param>
		/// <param name="provider">The <see cref="IServiceProvider"/> to get services from.</param>
		public ExecutionBase(IItemSource<TItem> source, IServiceProvider provider)
		{
			this.source = source ?? throw new ArgumentNullException(nameof(source));
			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
			this.logger = provider.GetRequiredService<ILogger<ExecutionBase<TConfig, TItem>>>();
		}

		/// <summary>
		/// Executes an <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="token">The token to trigger cancellation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous execution of this method.</returns>
		protected override async Task ExecuteInternalAsync(CancellationToken token)
		{
			Start(Processor.Config.__filePath);

			var stopwatch = new Stopwatch();

			try
			{
				// ==========================
				// 1. Initialize
				// ==========================

				SetState(ExecutionState.Initializing);
				logger.LogDebug($"Calling {nameof(Processor.InitializeAsync)}");
				stopwatch.Restart();
				Exception exception = null;

				try
				{
					await Processor.InitializeAsync();
					logger.LogDebug($"{nameof(Processor.InitializeAsync)} completed successfully");
				}
				catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
				{
					exception = ex;
					logger.LogError(ex, $"{nameof(Processor.InitializeAsync)} threw an excpetion: " + ex.ToString());
				}

				stopwatch.Stop();
				CompleteMethod(ProcessorMethod.InitializeAsync, stopwatch.Elapsed, exception);

				if (exception != null)
				{
					Fail(ProcessorMethod.InitializeAsync, exception);
					throw new ExecutionException(this.FailedIn.Value, exception);
				}

				// ==========================
				// 2. Get Items
				// ==========================

				SetState(ExecutionState.GettingItemsToProcess);
				logger.LogDebug($"Calling {nameof(source.GetItemsAsync)}");
				stopwatch.Restart();
				IAsyncEnumerable<TItem> items = null;

				try
				{
					items = source.GetItemsAsync();

					logger.LogDebug($"{nameof(source.GetItemsAsync)} completed successfully");
				}
				catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
				{
					exception = ex;
					logger.LogError(ex, $"Unhandled exception in {nameof(source.GetItemsAsync)}");
				}

				stopwatch.Stop();
				CompleteMethod(ProcessorMethod.GetItemsAsync, stopwatch.Elapsed, exception);

				if (exception != null)
				{
					Fail(ProcessorMethod.GetItemsAsync, exception);
					throw new ExecutionException(this.FailedIn.Value, exception);
				}

				// ==========================
				// 3. Get Enumerator
				// ==========================

				logger.LogDebug($"Calling {nameof(items.GetAsyncEnumerator)}");
				stopwatch.Restart();

				try
				{
					enumerator = items.GetAsyncEnumerator(Processor.CancellationToken);

					// If we're working with a Task<IEnumerable> wrapped in an 
					// AsyncEnumerableWrapper, let's await the task now.
					if (enumerator is AsyncEnumeratorWrapper<TItem>)
						await ((AsyncEnumeratorWrapper<TItem>)enumerator).GetInnerEnumerator();

					logger.LogDebug($"{nameof(items.GetAsyncEnumerator)} completed successfully");
				}
				catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
				{
					exception = ex;
					logger.LogError(ex, $"Unhandled exception in {nameof(items.GetAsyncEnumerator)}");
				}

				stopwatch.Stop();
				CompleteMethod(ProcessorMethod.GetEnumerator, stopwatch.Elapsed, exception);

				if (exception != null)
				{
					Fail(ProcessorMethod.GetEnumerator, exception);
					throw new ExecutionException(this.FailedIn.Value, exception);
				}

				// ==========================
				// 4. Count Items
				// ==========================

				logger.LogDebug($"Calling items.Count");
				stopwatch.Restart();

				if (items is AsyncEnumerableWrapper<TItem> wrapper && wrapper.CanBeCounted)
				{
					try
					{ 
						// If we're working with a Task<IEnumerable> wrapped in an AsyncEnumerableWrapper,
						// we can count items using the inner enumerable (intialized above).
						TotalItemCount = (await wrapper.GetInnerEnumerable()).Count();
					}
					catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
					{
						exception = ex;
						logger.LogError(ex, $"Unhandled exception in items.Count");
					}
				}

				stopwatch.Stop();
				CompleteMethod(ProcessorMethod.Count, stopwatch.Elapsed, exception);

				if (exception != null)
				{
					Fail(ProcessorMethod.Count, exception);
					throw new ExecutionException(this.FailedIn.Value, exception);
				}

				// ==========================
				// 5. Processor
				// ==========================

				SetState(ExecutionState.Processing);

				await ExecuteParallelTasksAsync();

				if (IsFailed)
					throw new ExecutionException(FailedIn.Value, FailedBecauseOf);
			}
			catch (ExecutionException) when (Processor.Config.Execution.HandleExceptions) 
			{ 
				// This catch block is left empty intentionally. Do not remove!
				// When HandleExceptions is true the inner try/catch blocks will handle all
				// exceptions thrown from a Processor and record them in the results. After
				// recording the results an ExecutionException is thrown to abort the remaining
				// work (except when items are retried or the Processor failure count has not
				// been exceeded). Since the exception has already been recorded there is nothing
				// do to here except prevent this exception from propagating to the caller.
				// Finalize is guaranteed to be called, so it alone is in the finally block.
			}
			finally
			{
				// ==========================
				// 6. Finalize
				// ==========================

				SetState(ExecutionState.Finalizing);
				logger.LogDebug($"Calling {nameof(Processor.FinalizeAsync)}");
				stopwatch.Restart();
				Exception exception = null;
				object output = null;

				try
				{
					output = await Processor.FinalizeAsync(Disposition);
					logger.LogDebug($"{nameof(Processor.FinalizeAsync)} completed successfully");
				}
				catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
				{
					exception = ex;
					logger.LogError(ex, $"Unhandled excpetion in {nameof(Processor.FinalizeAsync)}");
				}

				stopwatch.Stop();
				CompleteMethod(ProcessorMethod.FinalizeAsync, stopwatch.Elapsed, exception);

				if (exception != null)
					Fail(ProcessorMethod.FinalizeAsync, exception);
				
				Complete(output);

				if (exception != null && !Processor.Config.Execution.HandleExceptions)
					throw new ExecutionException(this.FailedIn.Value, exception);
			}
		}

		/// <summary>
		/// Performs the parallel execution of items.
		/// </summary>
		/// <returns>A <see cref="Task"/> representing the asynchronous execution of this method.</returns>
		async Task ExecuteParallelTasksAsync()
		{
			int parallelTaskCount = Processor.Config.Execution.ParallelTaskCount;
			
			if (!Processor.Options.CanProcessInParallel && parallelTaskCount > 1)
			{
				logger.LogWarning($"Ignoring Execution.ParallelTaskCount and running on one task because the Processor option CanProcessInParallel is false.");
				parallelTaskCount = 1;
			}

			if (parallelTaskCount < 1)
				parallelTaskCount = 1;

			logger.LogDebug($"Starting {parallelTaskCount} tasks to call {nameof(ExecutionBase<TConfig, TItem>.ProcessescopeAsync)}");

			List<Task> tasks = new List<Task>();

			for (int i = 0; i < parallelTaskCount; i++)
			{
				tasks.Add(Task.Run(async () =>
				{
					try
					{
						using var scope = provider.CreateAsyncScope();

                        await ProcessescopeAsync(scope);
					}
					catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
					{
						logger.LogError(ex, $"Unhandled exception in {nameof(ExecutionBase<TConfig, TItem>.ProcessescopeAsync)}");
					}
				}));
			}

			await Task.WhenAll(tasks);

			logger.LogDebug($"All tasks calling {nameof(ExecutionBase<TConfig, TItem>.ProcessescopeAsync)} completed successfully");
		}

		/// <summary>
		/// The entry point for a single thread processing items.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous execution of this method.</returns>
		async Task ProcessescopeAsync(AsyncServiceScope scope)
		{
			bool @continue = true;
			var stopwatch = new Stopwatch();
			int maxProcessAttempts = Processor.Config.Execution.ItemFailureRetryCount + 1;

			if (maxProcessAttempts < 1)
				maxProcessAttempts = 1;

			do
			{
				stopwatch.Restart();
				TItem item = default;
				ItemResult result = null;
				Exception exception = null;
				logger.LogDebug($"Calling {nameof(enumerator.MoveNextAsync)}");

				// ==========================
				// 1. Move Next
				// ==========================

				await moveNextLock.WaitAsync(Processor.CancellationToken);
				try
				{
					try
					{
						if (Processor.CancellationToken.IsCancellationRequested)
							@continue = false;
						else
							@continue = await enumerator.MoveNextAsync();
					}
					catch (OperationCanceledException ex) when (Processor.CancellationToken.IsCancellationRequested)
					{
						@continue = false;
						logger.LogError(ex, $"Unhandled {nameof(OperationCanceledException)} in {nameof(enumerator.MoveNextAsync)}");
					}
					catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
					{
						exception = ex;
						logger.LogError(ex, $"Unhandled exception in {nameof(enumerator.MoveNextAsync)}");
					}

					stopwatch.Stop();

					if (@continue)
					{
						// Add a result only if we're not at the end of the enumerator
						result = new ItemResult();
						result.MoveNext(stopwatch.Elapsed, exception);
					}

					if (exception != null)
					{
						FailItem(result, ProcessorMethod.EnumeratorMoveNext, exception);
						break;
					}

					if (!@continue)
						break;

					// ==========================
					// 2. Get Item
					// ==========================

					stopwatch.Restart();

					try
					{
						item = enumerator.Current;
					}
					catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
					{
						exception = ex;
						logger.LogError(ex, $"Unhandled exception in {nameof(enumerator.Current)}");
					}

					stopwatch.Stop();
					result.Current(stopwatch.Elapsed, exception);

					if (exception != null)
					{
						FailItem(result, ProcessorMethod.EnumeratorCurrent, exception);
						break;
					}
				}
				finally
				{
					moveNextLock.Release();
				}

				// ==========================
				// 3. Get Item ID
				// ==========================

				stopwatch.Restart();
				string id = null;

				try
				{
					id = await source.GetItemIdAsync(item);
				}
				catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
				{
					logger.LogError(ex, $"Unhandled exception in {nameof(source.GetItemIdAsync)}");
					exception = ex;
				}

				stopwatch.Stop();
				result.SetItem(id, stopwatch.Elapsed, exception);

				if (exception != null)
				{
					FailItem(result, ProcessorMethod.GetItemIdAsync, exception);
					break;
				}

				// ==========================
				// 4. Processor
				// ==========================

				stopwatch.Restart();
				Result processItem = null;

				for (int i = 0; i < maxProcessAttempts; i++)
				{
					exception = null;

					try
					{
						processItem = await CallProcess(scope, item);
					}
					catch (Exception ex) when (Processor.Config.Execution.HandleExceptions)
					{
						exception = ex;
						logger.LogError(ex, $"Unhandled exception in {nameof(CallProcess)}");
					}

					if (processItem != null && processItem.IsSuccessful)
						break;
				}

				stopwatch.Stop();
				result.Complete(processItem, stopwatch.Elapsed, exception);

				CompleteProcessing(result, exception);

			} while (@continue && !IsFailed && !IsCanceled);

			stopwatch.Stop();
		}

		/// <summary>
		/// Calls the ProcessAsync method whose signature is unique to the <see cref="Execution"/> and <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="scope">The <see cref="IServiceScope"/> containing a scoped <see cref="IServiceProvider"/> to get services from.</param>
		/// <param name="item">The item to be processed.</param>
		/// <returns>The <see cref="Result"/> returned from ProcessAsync.</returns>
		protected abstract Task<Result> CallProcess(IServiceScope scope, TItem item);
	}
}
