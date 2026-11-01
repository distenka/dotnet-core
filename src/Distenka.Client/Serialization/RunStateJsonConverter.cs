using Distenka.Client.Runs;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Distenka.Client.Serialization
{
	/// <summary>
	/// Reads <see cref="RunState"/> from its name, case-insensitively; an unknown name becomes <see cref="RunState.Unknown"/>
	/// (ADR-008 §2.2). Numbers are refused — the platform never sends numeric states. Writes the PascalCase name.
	/// </summary>
	sealed class RunStateJsonConverter : JsonConverter<RunState>
	{
		public override RunState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.String)
				throw new JsonException($"A run state must be a JSON string; got {reader.TokenType}.");

			return RunStateNames.Parse(reader.GetString());
		}

		public override void Write(Utf8JsonWriter writer, RunState value, JsonSerializerOptions options) =>
			writer.WriteStringValue(RunStateNames.Name(value));
	}

	/// <summary>Name ↔ <see cref="RunState"/> without <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>, which would accept digits.</summary>
	static class RunStateNames
	{
		static readonly RunState[] Known =
		{
			RunState.Accepted, RunState.Queued, RunState.Dispatching, RunState.Running, RunState.Completed,
			RunState.Failed, RunState.Cancelled, RunState.TimedOut, RunState.Expired,
		};

		internal static RunState Parse(string? name)
		{
			if (name == null)
				return RunState.Unknown;

			var trimmed = name.Trim();
			foreach (var state in Known)
			{
				if (string.Equals(Name(state), trimmed, StringComparison.OrdinalIgnoreCase))
					return state;
			}
			return RunState.Unknown;
		}

		internal static string Name(RunState state) => state switch
		{
			RunState.Accepted => "Accepted",
			RunState.Queued => "Queued",
			RunState.Dispatching => "Dispatching",
			RunState.Running => "Running",
			RunState.Completed => "Completed",
			RunState.Failed => "Failed",
			RunState.Cancelled => "Cancelled",
			RunState.TimedOut => "TimedOut",
			RunState.Expired => "Expired",
			_ => "Unknown",
		};
	}
}
