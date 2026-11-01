using Distenka.Client.Internal;

namespace Distenka.Client.Runs
{
	sealed class RunsClient : IRunsClient
	{
		internal RunsClient(HttpTransport transport) => Transport = transport;

		internal HttpTransport Transport { get; }
	}
}
