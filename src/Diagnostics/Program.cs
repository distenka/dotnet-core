using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace Distenka.Diagnostics
{
	class Program
	{
		static Task Main(string[] args)
		{
			return ProcessorHost.CreateDefaultBuilder(args)
				.Build()
				.RunAsync();
		}
	}
}
