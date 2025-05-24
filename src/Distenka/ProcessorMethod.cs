namespace Distenka
{
	/// <summary>
	/// A list of methods found on a Processor.
	/// </summary>
	public enum ProcessorMethod
	{
		/// <summary>
		/// The <see cref="IProcessor.InitializeAsync"/> method.
		/// </summary>
		InitializeAsync,

		/// <summary>
		/// The GetItemsAsync method.
		/// </summary>
		GetItemsAsync,

		/// <summary>
		/// The Count operation performed on an enumerable list of items.
		/// </summary>
		Count,

		/// <summary>
		/// The GetEnumeratorAsync method on an enumerable list of items.
		/// </summary>
		GetEnumerator,

		/// <summary>
		/// The MoveNextAsync method on IAsyncEnumerator.
		/// </summary>
		EnumeratorMoveNext,

		/// <summary>
		/// The Current property on IAsyncEnumerator.
		/// </summary>
		EnumeratorCurrent,

		/// <summary>
		/// The GetItemIdAsync method.
		/// </summary>
		GetItemIdAsync,

		/// <summary>
		/// TheProcessAsync method.
		/// </summary>
		ProcessAsync,

		/// <summary>
		/// The <see cref="IProcessor.FinalizeAsync(Disposition)"/> method.
		/// </summary>
		FinalizeAsync
	}
}
