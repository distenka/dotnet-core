using System.Collections.Generic;
using System.Threading.Tasks;

namespace Distenka.Processing
{
	/// <summary>
	/// Used by <see cref="Processoror{TConfig}"/> to call <see cref="Processoror{TConfig}.ProcessAsync"/> a single time.
	/// </summary>
	internal class SingleItemSource : IItemSource<string>
	{
		public bool CanCountItems => true;

		public Task<string> GetItemIdAsync(string item) => Task.FromResult(item);

		public IAsyncEnumerable<string> GetItemsAsync() => new string[] { nameof(Processor<Config>.ProcessAsync) }.ToAsyncEnumerable();
	}
}
