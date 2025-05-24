using System;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka
{
	/// <summary>
	/// The interface that all Distenka Processes must implement.
	/// </summary>
	/// <remarks>
	/// Do not implement this interface directly, instead you should extend one of the Distenka.Processor classes in the Distenka library.
	/// </remarks>
	public interface IProcessor
	{
		/// <summary>
		/// Gets the Processor's config.
		/// </summary>
		Config Config { get; }

		/// <summary>
		/// Gets the options for the Processor, determining how the Processor can be run.
		/// </summary>
		ProcessorOptions Options { get; }

		/// <summary>
		/// Gets the <see cref="CancellationToken"/> that the Processor should use when running.
		/// </summary>
		CancellationToken CancellationToken { get; set; }

		/// <summary>
		/// Gets the <see cref="Execution"/> implementation that can run the Processor.
		/// </summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		Execution GetExecution(IServiceProvider provider);

		/// <summary>
		/// Gets the default config for the Processor.
		/// </summary>
		/// <returns></returns>
		Config GetDefaultConfig();

		/// <summary>
		/// Initializes the Processor, being called before any items are processed.
		/// </summary>
		/// <returns></returns>
		Task InitializeAsync();

		/// <summary>
		/// Finalizes the Processor, being called after all items have been processed.
		/// </summary>
		/// <param name="dispotion"></param>
		/// <returns></returns>
		Task<object> FinalizeAsync(Disposition dispotion);
	}
}
