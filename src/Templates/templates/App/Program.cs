using Distenka;
using System.Threading.Tasks;

namespace NewDistenkaApp
{
	class Program
	{
		static async Task Main(string[] args)
		{
			await ProcessorHost.CreateDefaultBuilder(args)
				.Build()
				.RunProcessorAsync();
		}
	}
}
