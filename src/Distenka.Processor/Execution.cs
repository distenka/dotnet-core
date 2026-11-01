using Distenka.Testing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka
{
	/// <summary>
	/// Executes a <see cref="IProcessor"/>, communicating with the Distenka API, handling exceptions, and providing status.
	/// </summary>
	public abstract class Execution
	{
		/// <summary>
		/// Gets the overall and last minute average time for an item to be processed.
		/// </summary>
		public Average ItemProcessingTime { get; } = new Average("Item Processing Time");

        /// <summary>
        /// Gets a list of <see cref="MethodOutcome">MethodOutcomes</see> for each method of a Processor, excluding methods executed per item.
        /// </summary>
        public Dictionary<ProcessorMethod, MethodOutcome> Methods { get; } = new Dictionary<ProcessorMethod, MethodOutcome>();

        /// <summary>
        /// Gets the <see cref="MethodOutcome"/> for InitializeAsync.
        /// </summary>
        public MethodOutcome InitializeAsync => Methods.ValueOrDefault(ProcessorMethod.InitializeAsync);

        /// <summary>
        /// Gets the <see cref="MethodOutcome"/> for GetItemsAsync.
        /// </summary>
        public MethodOutcome GetItemsAsync => Methods.ValueOrDefault(ProcessorMethod.GetItemsAsync);

        /// <summary>
        /// Gets the <see cref="MethodOutcome"/> for Count.
        /// </summary>
        public MethodOutcome Count => Methods.ValueOrDefault(ProcessorMethod.Count);

        /// <summary>
        /// Gets the <see cref="MethodOutcome"/> for GetEnumerator.
        /// </summary>
        public MethodOutcome GetEnumerator => Methods.ValueOrDefault(ProcessorMethod.GetEnumerator);

        /// <summary>
        /// Gets the <see cref="MethodOutcome"/> for FinalizeAsync.
        /// </summary>
        public MethodOutcome FinalizeAsync => Methods.ValueOrDefault(ProcessorMethod.FinalizeAsync);

        /// <summary>
        /// Event raised when execution of a Processor starts.
        /// </summary>
        public event Func<string, DateTime, Task> Started;

		/// <summary>
		/// Event raised when the state of the Processor changes.
		/// </summary>
		public event Func<ExecutionState, Task> StateChanged;

		/// <summary>
		/// Event raised when an processing an item completes.
		/// </summary>
		public event Func<ItemResult, Task> ItemCompleted;

		/// <summary>
		/// Event raised when a method completes.
		/// </summary>
		public event Func<MethodOutcome, Task> MethodCompleted;

		/// <summary>
		/// Event raised when execution of a Processor is complete.
		/// </summary>
		public event Func<object, Disposition, DateTime, Task> Completed;

		/// <summary>
		/// The <see cref="IProcessor"/> being executed.
		/// </summary>
		public abstract IProcessor Processor { get; }

		/// <summary>
		/// The total number of items to be processed.
		/// </summary>
		/// <remarks>
		/// <see cref="TotalItemCount"/> will be null when a <see cref="IProcessor"/> does not use ToAsyncEnumerable with canCountItems 
		/// set to true. The difference between this total and the sum of <see cref="SuccessfulItemCount"/> and <see cref="FailedItemCount"/>
		/// is the number of items yet to be processed or not processed in the case of a Processor that ended in the <see cref="Disposition.Failed"/> state.
		/// </remarks>
		public int? TotalItemCount { get; protected set; }

		/// <summary>
		/// The number of items processed.
		/// </summary>
		public int CompletedItemCount => successfulItemCount + failedItemCount;

		int successfulItemCount, failedItemCount;

		/// <summary>
		/// The number of items processed with a successful (<see cref="Result.IsSuccessful"/>) <see cref="Result"/>.
		/// </summary>
		public int SuccessfulItemCount => successfulItemCount;

		/// <summary>
		/// The number of items processed with an unsuccessful (not <see cref="Result.IsSuccessful"/>) <see cref="Result"/>.
		/// </summary>
		public int FailedItemCount => failedItemCount;

		readonly ConcurrentDictionary<(string, bool), ItemCategory> categories = new ConcurrentDictionary<(string, bool), ItemCategory>();

		/// <summary>
		/// Gets the completed item category results.
		/// </summary>
		public IEnumerable<ItemCategory> ItemCategories => categories.Values;

		/// <summary>
		/// Gets the <see cref="ExecutionState">state</see> of execution.
		/// </summary>
		public ExecutionState State { get; private set; }

		/// <summary>
		/// The UTC time at which the run started.
		/// </summary>
		public DateTime? StartedAt { get; internal set; }

		/// <summary>
		/// The UTC time at which the run ended.
		/// </summary>
		public DateTime? CompletedAt { get; internal set; }

		/// <summary>
		/// Indicates whether the execution has completed.
		/// </summary>
		public bool IsComplete => CompletedAt.HasValue;

		/// <summary>
		/// The final state of the Processor.
		/// </summary>
		public Disposition Disposition =>
			IsFailed ? Disposition.Failed :
			IsCanceled ? Disposition.Cancelled :
			Disposition.Successful;

		/// <summary>
		/// Indicates whether the Processor has been cancelled or is in the process of cancelling.
		/// </summary>
		public bool IsCanceled => CanceledAt.HasValue;

		/// <summary>
		/// The UTC time at which execution of the Processor was cancelled.
		/// </summary>
		public DateTime? CanceledAt { get; private set; }

		/// <summary>
		/// Indicates whether the Processor has failed or in the process of failing. 
		/// </summary>
		public bool IsFailed => FailedAt.HasValue;

		/// <summary>
		/// The UTC time at which execution of the Processor failed.
		/// </summary>
		public DateTime? FailedAt { get; private set; }

		/// <summary>
		/// The <see cref="ProcessorMethod">method</see> that was executing when the Processor failed.
		/// </summary>
		public ProcessorMethod? FailedIn { get; private set; }

		/// <summary>
		/// The exception that caused the Processor to fail.
		/// </summary>
		public Exception FailedBecauseOf { get; private set; }

		CancellationTokenSource ProcessCancellation;

		/// <summary>
		/// Executes the <see cref="Processor"/>.
		/// </summary>
		/// <param name="token">Optional <see cref="CancellationToken"/></param>
		public async Task ExecuteAsync(CancellationToken token)
		{
			ProcessCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
			Processor.CancellationToken = ProcessCancellation.Token;
			Processor.CancellationToken.Register(new Action(OnCancel));

			CancellationTokenSource eventPumpSource = new CancellationTokenSource();
			var eventPump = RunEventPump(eventPumpSource.Token);

			await ExecuteInternalAsync(token);

			eventPumpSource.Cancel();
			await eventPump;

			// Make sure all the events are raised
			await PublishEvents();
		}

		/// <summary>
		/// Executes an <see cref="IProcessor"/>.
		/// </summary>
		/// <param name="token">The token to trigger cancellation.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous execution of this method.</returns>
		protected abstract Task ExecuteInternalAsync(CancellationToken token);

		int cancelAfter;

		/// <summary>
		/// Causes the <see cref="Execution"/> to be cancelled once the <see cref="CompletedItemCount"/> reaches the <paramref name="numberOfItems"/> set.
		/// </summary>
		/// <remarks>Setting <paramref name="numberOfItems"/> to zero causes the Processor to execute normally.</remarks>
		public void CancelAfter(int numberOfItems)
		{
			this.cancelAfter = numberOfItems;
		}

		/// <summary>
		/// Cancels the execution of the Processor if the number of items processed has met or exceeded the cancellation threshold set by <see cref="CancelAfter(int)"/>.
		/// </summary>
		protected void EnforceCancelAfter()
		{
			if (cancelAfter > 0 && CompletedItemCount >= cancelAfter)
				ProcessCancellation.Cancel();
		}

		/// <summary>
		/// Initializes the <see cref="Execution"/>.
		/// </summary>
		protected Execution()
		{
			this.State = ExecutionState.NotStarted;
		}

		readonly ConcurrentQueue<object> events = new ConcurrentQueue<object>();

		class StartInfo
		{
			internal string Config { get; set; }
		}

		class CompleteInfo
		{
			internal object Output { get; set; }
		}

		class StateChange
		{
			internal ExecutionState State { get; set; }
		}

		/// <summary>
		/// Called internally to mark the start of the execution.
		/// </summary>
		/// <param name="config"></param>
		protected void Start(string config)
		{
			StartedAt = DateTime.UtcNow;
			events.Enqueue(new StartInfo() { Config = config });
		}

		/// <summary>
		/// Called internally to set the state of the execution.
		/// </summary>
		/// <param name="state"></param>
		protected void SetState(ExecutionState state)
		{
			if (State != state)
			{
				State = state;
				events.Enqueue(new StateChange() { State = state });
			}
		}

		/// <summary>
		/// Called internally to mark the completion of a call to a Processor method.
		/// </summary>
		protected void CompleteMethod(ProcessorMethod method, TimeSpan duration, Exception exception)
		{
			var mo = new MethodOutcome(method, duration, exception);
			Methods.Add(method, mo);
			events.Enqueue(mo);
		}

		/// <summary>
		/// Called internally to mark the completion of an item being processed.
		/// </summary>
		/// <param name="result"></param>
		protected void CompleteItem(ItemResult result)
		{
			if (result.IsSuccessful)
				Interlocked.Increment(ref successfulItemCount);
			else
				Interlocked.Increment(ref failedItemCount);

			var cat = categories.GetOrAdd((result.Category, result.IsSuccessful), new ItemCategory(0, result.IsSuccessful, result.Category));

			Interlocked.Increment(ref cat.count);

			events.Enqueue(result);
		}

		/// <summary>
		/// Called internally to mark the completion of the execution.
		/// </summary>
		protected void Complete(object output)
		{
			SetState(ExecutionState.Complete);
			CompletedAt = DateTime.UtcNow;
			events.Enqueue(new CompleteInfo() { Output = output });
		}

		private async Task RunEventPump(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await PublishEvents();

				ItemProcessingTime.Purge();
				
				await Task.Delay(50);
			}

            await PublishEvents();

            ItemProcessingTime.Purge();
        }

		private async Task PublishEvents()
		{
			object @event;

			while (events.TryDequeue(out @event))
			{
				try
				{
					if (@event is ItemResult r)
					{
						if (r.IsSuccessful)
							ItemProcessingTime.RecordSuccess(r.ProcessAsync.Duration);
						else
							ItemProcessingTime.RecordError();
					}

					await (@event switch
					{
						ItemResult result => ItemCompleted != null ? ItemCompleted(result) : Task.CompletedTask,
						MethodOutcome method => MethodCompleted != null ? MethodCompleted(method) : Task.CompletedTask,
						StateChange state => StateChanged != null ? StateChanged(state.State) : Task.CompletedTask,
						StartInfo start => Started != null ? Started(start.Config, StartedAt.Value) : Task.CompletedTask,
						CompleteInfo complete => Completed != null ? Completed(complete.Output, this.Disposition, CompletedAt.Value) : Task.CompletedTask,
						_ => throw new NotSupportedException(),
					});
				}
				catch
				{
					// Don't let an external event handler ruin our life.
					// Maybe this should be logged, but that adds a new dependency to this project.
				}
			}
		}

		private void OnCancel()
		{
			if (State != ExecutionState.Complete && !IsCanceled && !IsFailed)
				CanceledAt = DateTime.UtcNow;
		}

		/// <summary>
		/// Called internally to complete the processing of an item and fail or cancel the Processor when appropriate.
		/// </summary>
		/// <param name="result"></param>
		/// <param name="exception"></param>
		protected void CompleteProcessing(ItemResult result, Exception exception)
		{
			// CompleteItem needs to be called first, this increments FailedItemCount
			CompleteItem(result);

			if (FailedItemCount >= Math.Max(1, Processor.Config.Execution.ItemFailureCountToStopProcess))
				Fail(ProcessorMethod.ProcessAsync, exception);

			EnforceCancelAfter();
		}

		/// <summary>
		/// Called internally to fail the execution when the <see cref="ExecutionConfig.ItemFailureCountToStopProcess"/> threshold has been met or exceeded.
		/// </summary>
		/// <param name="result"></param>
		/// <param name="failedIn"></param>
		/// <param name="exception"></param>
		protected void FailItem(ItemResult result, ProcessorMethod failedIn, Exception exception)
		{
			CompleteItem(result);
			Fail(failedIn, exception);
		}

		/// <summary>
		/// Called internally to fail the execution when a Processor method throws an exception.
		/// </summary>
		protected void Fail(ProcessorMethod failedIn, Exception exception)
		{
			if (!IsFailed)
			{
				FailedAt = DateTime.UtcNow;
				FailedIn = failedIn;
				FailedBecauseOf = exception;

				ProcessCancellation.Cancel();
			}
		}
	}
}