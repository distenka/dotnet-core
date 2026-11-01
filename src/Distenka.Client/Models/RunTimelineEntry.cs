using Distenka.Client.Serialization;
using System;
using System.Text.Json.Serialization;

namespace Distenka.Client.Runs
{
	/// <summary>The level of a <see cref="RunTimelineEntry"/>, from the <c>LEVEL:</c> prefix the processor wrote.</summary>
	public enum RunLogLevel
	{
		/// <summary>A level this version of the SDK does not know.</summary>
		Unknown = 0,

		/// <summary><c>INFO</c>.</summary>
		Info,

		/// <summary><c>WARN</c>.</summary>
		Warn,

		/// <summary><c>ERROR</c> — on the platform, an error line fails the run (ADR-004).</summary>
		Error,
	}

	/// <summary>
	/// One line of a run's execution log, as the platform recorded it (<c>details.timeline</c>, platform <c>RunDto.Step</c>).
	/// Built from the executor's captured log lines, or — for runs archived before structured logs — split from the
	/// plain-text output, in which case <see cref="Level"/> is <see langword="null"/>.
	/// </summary>
	public sealed class RunTimelineEntry
	{
		/// <summary>Creates the value (used by the serializer; also handy in tests).</summary>
		[JsonConstructor]
		public RunTimelineEntry(DateTimeOffset? timestamp, string? message, string? levelName, TimeSpan? elapsed)
		{
			Timestamp = timestamp;
			Message = message;
			LevelName = levelName;
			Elapsed = elapsed;
		}

		/// <summary>When the line was written.</summary>
		public DateTimeOffset? Timestamp { get; }

		/// <summary>The line's text, without its level prefix.</summary>
		public string? Message { get; }

		/// <summary>The level parsed from <see cref="LevelName"/>; <see langword="null"/> for legacy plain-text entries.</summary>
		[JsonIgnore]
		public RunLogLevel? Level => ParseLevel(LevelName);

		/// <summary>The <c>level</c> exactly as the platform sent it (<c>INFO</c>, <c>WARN</c>, <c>ERROR</c> or <see langword="null"/>).</summary>
		[JsonPropertyName("level")]
		public string? LevelName { get; }

		/// <summary>Time since the run started (the platform sends milliseconds).</summary>
		[JsonConverter(typeof(MillisecondsTimeSpanJsonConverter))]
		public TimeSpan? Elapsed { get; }

		static RunLogLevel? ParseLevel(string? name)
		{
			if (name is not string value)
				return null;

			var level = value.Trim();
			if (string.Equals(level, "INFO", StringComparison.OrdinalIgnoreCase))
				return RunLogLevel.Info;
			if (string.Equals(level, "WARN", StringComparison.OrdinalIgnoreCase) || string.Equals(level, "WARNING", StringComparison.OrdinalIgnoreCase))
				return RunLogLevel.Warn;
			if (string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase))
				return RunLogLevel.Error;
			return RunLogLevel.Unknown;
		}
	}
}
