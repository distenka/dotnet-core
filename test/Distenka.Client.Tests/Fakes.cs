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

	/// <summary>Captures every formatted log message and its category, at all levels (DCS-027, DCS-080).</summary>
	sealed class CapturingLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
	{
		readonly List<(string Category, string Message)> entries = new List<(string Category, string Message)>();

		public IReadOnlyList<string> Messages
		{
			get { lock (entries) return entries.ConvertAll(e => e.Message); }
		}

		public IReadOnlyList<(string Category, string Message)> Entries
		{
			get { lock (entries) return entries.ToArray(); }
		}

		public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

		public void Dispose() { }

		sealed class Logger : Microsoft.Extensions.Logging.ILogger
		{
			readonly CapturingLoggerProvider owner;
			readonly string category;

			public Logger(CapturingLoggerProvider owner, string category)
			{
				this.owner = owner;
				this.category = category;
			}

			public IDisposable BeginScope<TState>(TState state) => null;

			public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

			public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
				Exception exception, Func<TState, Exception, string> formatter)
			{
				lock (owner.entries)
					owner.entries.Add((category, formatter(state, exception)));
			}
		}
	}
}
