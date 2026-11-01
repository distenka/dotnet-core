using Distenka.Client.Authentication;
using System;
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

	/// <summary>
	/// Answers each request with the next scripted response and records the request plus its body as text (read at send
	/// time: the transport disposes each request message afterwards).
	/// </summary>
	sealed class ScriptedHttpHandler : HttpMessageHandler
	{
		readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> script;

		public ScriptedHttpHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) =>
			script = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(steps);

		public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

		public List<string> Bodies { get; } = new List<string>();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);
			Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
			if (script.Count == 0)
				throw new InvalidOperationException("Unexpected extra request.");
			var response = script.Dequeue()(request);
			response.RequestMessage = request;
			return response;
		}

		/// <summary>A response with <paramref name="status"/>, an optional JSON body and headers.</summary>
		public static Func<HttpRequestMessage, HttpResponseMessage> Reply(int status, string json = null, params (string Name, string Value)[] headers) => _ =>
		{
			var response = new HttpResponseMessage((HttpStatusCode)status);
			if (json != null)
				response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
			foreach (var (name, value) in headers)
				response.Headers.TryAddWithoutValidation(name, value);
			return response;
		};
	}
}
