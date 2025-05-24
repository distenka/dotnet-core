using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Testing
{
	/// <summary>
	/// A test run of a Processor, built by a <see cref="TestHost{TProcess}"/>.
	/// </summary>
	/// <typeparam name="TProcess">The type of <see cref="IProcessor"/> being tested.</typeparam>
	public class TestRun<TProcess> : IDisposable
		where TProcess : IProcessor
	{
		CancellationTokenSource cts;

		/// <summary>
		/// The <see cref="IServiceProvider"/> used to run the <see cref="Process"/>.
		/// </summary>
		public IServiceProvider Services { get; }

		/// <summary>
		/// The Processor being tested.
		/// </summary>
		public TProcess Process { get; }

		/// <summary>
		/// The <see cref="Execution"/> implementation that ran the Processor.
		/// </summary>
		public Execution Execution { get; }

		/// <summary>
		/// A log of results emitted from the Processor during execution.
		/// </summary>
		public ResultLog Results { get; }

		internal TestRun(TProcess Process, Execution execution, ResultLog results, ServiceProvider services) => (Process, Execution, Results, Services) = (Process, execution, results, services);

		/// <summary>
		/// Performs clean up.
		/// </summary>
		public void Dispose() => (Services as IDisposable)?.Dispose();

		/// <summary>
		/// Triggers cancellation of a running Processor.
		/// </summary>
		public void Cancel()
		{
			if (cts == null)
				throw new InvalidOperationException($"{nameof(Cancel)} cannot be called before {nameof(RunAsync)}");

			cts.Cancel();
		}

		/// <summary>
		/// Runs the Processor.
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> to trigger cancellation.</param>
		/// <param name="cancelAfter">When a positive value is given, cancels the Processor after that many items have been processed. This is useful to stop a service after an expected number of items have been processed in a unit test.</param>
		public async Task<TestRun<TProcess>> RunAsync(CancellationToken cancellationToken = default, int cancelAfter = 0)
		{
			if (cts != null)
				throw new InvalidOperationException($"{nameof(RunAsync)} cannot be called more than once");

			cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			Execution.CancelAfter(cancelAfter);
			await Execution.ExecuteAsync(cts.Token);

			return this;
		}
	}
}
