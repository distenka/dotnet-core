using Distenka.Client.Authentication;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Tests
{
	/// <summary>A credential returning a fixed token. Stands in for the DCS-021 credentials.</summary>
	sealed class FixedTokenCredential : IDistenkaCredential
	{
		readonly string token;

		public FixedTokenCredential(string token) => this.token = token;

		public CancellationToken LastCancellationToken { get; private set; }

		public int Calls { get; private set; }

		public ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken)
		{
			Calls++;
			LastCancellationToken = cancellationToken;
			return new ValueTask<string>(token);
		}

		/// <summary>Deliberately leaks the token, to prove no SDK message is built from a credential's ToString().</summary>
		public override string ToString() => token;
	}

	/// <summary>Records every request and answers 200 with an empty body.</summary>
	sealed class RecordingHandler : HttpMessageHandler
	{
		public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
		}
	}
}
