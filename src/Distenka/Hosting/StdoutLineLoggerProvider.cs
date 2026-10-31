using Microsoft.Extensions.Logging;
using Spectre.Console;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Distenka.Hosting
{
	/// <summary>
	/// Settings of the <see cref="StdoutLineLoggerProvider"/> (ADR-009 §2.1, §2.4).
	/// </summary>
	internal sealed class StdoutLineLoggerOptions
	{
		/// <summary>Configuration key of the neutralisation opt-out (ADR-009 §2.4 trade-off 5).</summary>
		public const string NeutraliseHeuristicTriggersKey = "Distenka:Logging:NeutraliseHeuristicTriggers";

		/// <summary>Plain mode (stdout redirected or <c>--plain</c>): lines go straight to <see cref="Console.Out"/>.</summary>
		public bool Plain { get; init; }

		/// <summary>
		/// Rewrites the executor's error triggers in DEBUG/INFO/WARN lines (plain mode only). Default <c>true</c>.
		/// </summary>
		public bool NeutraliseHeuristicTriggers { get; init; } = true;
	}

	/// <summary>
	/// Writes <see cref="ILogger"/> events to stdout as one executor-compatible line each (ADR-009 §2.2, DCS-042):
	/// <c>&lt;LEVEL&gt;: [&lt;Category&gt;] &lt;message&gt;[ | &lt;ExceptionType&gt;: &lt;message&gt;…][ | &lt;stack&gt;]</c>.
	/// Never writes to stderr. Has no dependency on <c>Microsoft.Extensions.Logging.Console</c>.
	/// </summary>
	[ProviderAlias("DistenkaStdout")]
	internal sealed class StdoutLineLoggerProvider : ILoggerProvider
	{
		readonly StdoutLineLoggerOptions _options;
		readonly Action<string> _write;

		public StdoutLineLoggerProvider(StdoutLineLoggerOptions options)
			: this(options, (options ?? throw new ArgumentNullException(nameof(options))).Plain ? WritePlain : WriteInteractive) { }

		internal StdoutLineLoggerProvider(StdoutLineLoggerOptions options, Action<string> write)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_write = write ?? throw new ArgumentNullException(nameof(write));
		}

		/// <summary>Neutralisation applies only in plain mode: on a terminal no executor reads the output (ADR-009 §2.1).</summary>
		internal bool Neutralise => _options.Plain && _options.NeutraliseHeuristicTriggers;

		public ILogger CreateLogger(string categoryName) =>
			new StdoutLineLogger(StdoutLineFormatter.ShortCategory(categoryName), this);

		internal void Write(string line) => _write(line);

		// Console.Out is a synchronized writer: one WriteLine call = one complete line, never interleaved (ADR-009 §2.2).
		static void WritePlain(string line) => Console.Out.WriteLine(line);

		// Through Spectre so the line does not corrupt a live Status/Progress display; WriteLine(string) does not parse markup.
		static void WriteInteractive(string line) => AnsiConsole.WriteLine(line);

		public void Dispose() { }

		sealed class StdoutLineLogger : ILogger
		{
			readonly string _category;
			readonly StdoutLineLoggerProvider _provider;

			public StdoutLineLogger(string category, StdoutLineLoggerProvider provider)
			{
				_category = category;
				_provider = provider;
			}

			public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

			IDisposable ILogger.BeginScope<TState>(TState state) => null;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				if (!IsEnabled(logLevel) || formatter is null)
					return;

				string message;
				try
				{
					message = formatter(state, exception);
				}
				catch (Exception ex)
				{
					message = $"(log message could not be formatted: {ex.GetType().Name})";
				}

				_provider.Write(StdoutLineFormatter.Format(logLevel, _category, message, exception, _provider.Neutralise));
			}
		}
	}

	/// <summary>The ADR-009 §2.2 line format and the §2.4 neutralisation rules.</summary>
	internal static class StdoutLineFormatter
	{
		static readonly Regex ErrorColon = new Regex("(error):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

		/// <summary>The executor-recognised prefix for a level (ADR-009 §2.2 table).</summary>
		public static string Prefix(LogLevel level) => level switch
		{
			LogLevel.Trace or LogLevel.Debug => "DEBUG:",
			LogLevel.Information => "INFO:",
			LogLevel.Warning => "WARN:",
			_ => "ERROR:"
		};

		/// <summary>The last segment of a logger category, without generic arity (<c>Ns.Foo`1</c> → <c>Foo</c>).</summary>
		public static string ShortCategory(string category)
		{
			if (string.IsNullOrEmpty(category))
				return string.Empty;

			var tick = category.IndexOf('`');
			if (tick >= 0)
				category = category.Substring(0, tick);

			var generic = category.IndexOf('<');
			if (generic >= 0)
				category = category.Substring(0, generic);

			var dot = category.LastIndexOf('.');
			return dot >= 0 ? category.Substring(dot + 1) : category;
		}

		/// <summary>
		/// Formats one event as one physical line: escaped, neutralised when asked and below Error, capped at
		/// <see cref="RunOutcome.MaxLineLength"/>. The stack trace is appended only on ERROR lines.
		/// </summary>
		public static string Format(LogLevel level, string shortCategory, string message, Exception exception, bool neutralise)
		{
			var isError = level >= LogLevel.Error;
			var body = new StringBuilder();

			if (!string.IsNullOrEmpty(shortCategory))
				body.Append('[').Append(shortCategory).Append("] ");

			body.Append(message ?? string.Empty);

			for (var e = exception; e != null; e = e.InnerException)
				body.Append(" | ").Append(e.GetType().FullName).Append(": ").Append(e.Message);

			if (isError && exception != null)
			{
				var stack = StackOf(exception);
				if (!string.IsNullOrWhiteSpace(stack))
					body.Append(" | ").Append(stack.Trim());
			}

			var text = RunOutcome.Escape(body.ToString());

			if (neutralise && !isError)
				text = Neutralise(text);

			return RunOutcome.Cap(Prefix(level) + " " + text);
		}

		/// <summary>
		/// ADR-009 §2.4: <c>Exception</c> → <c>exception</c> (only the capital E lowered); <c>error:</c> in any case →
		/// the same word, a space, then the colon. Nothing else changes.
		/// </summary>
		public static string Neutralise(string text)
		{
			if (string.IsNullOrEmpty(text))
				return text ?? string.Empty;

			text = text.Replace("Exception", "exception", StringComparison.Ordinal);
			return ErrorColon.Replace(text, "$1 :");
		}

		static string StackOf(Exception exception)
		{
			try
			{
				// Demystified, without mutating the author's exception (Exception.Demystify() would).
				return new EnhancedStackTrace(exception).ToString();
			}
			catch
			{
				return exception.StackTrace;
			}
		}
	}
}
