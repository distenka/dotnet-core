using Distenka.Client.Internal;

namespace Distenka.Client.Invocations
{
	sealed class InvocationsClient : IInvocationsClient
	{
		internal InvocationsClient(HttpTransport transport) => Transport = transport;

		internal HttpTransport Transport { get; }
	}
}
