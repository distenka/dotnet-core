using Distenka.Client.Invocations;
using Distenka.Client.Runs;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Distenka.Client.Serialization
{
	/// <summary>
	/// The one serializer context for every client contract (ADR-008 §2.1.3): source-generated, so no reflection-based
	/// serialization happens in the client. Web defaults (camelCase, case-insensitive read) with strict numbers, unknown
	/// members ignored and <see langword="null"/> members omitted on write (ADR-008 §2.1.4). Later stories register their
	/// models here.
	/// </summary>
	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web,
		NumberHandling = JsonNumberHandling.Strict,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonSerializable(typeof(RunAccepted))]
	[JsonSerializable(typeof(RunDetails))]
	[JsonSerializable(typeof(RunSummary))]
	[JsonSerializable(typeof(CancelResult))]
	[JsonSerializable(typeof(InvocationAccepted))]
	[JsonSerializable(typeof(RunResult))]
	[JsonSerializable(typeof(RunState))]
	[JsonSerializable(typeof(RunOutputSummary))]
	internal sealed partial class DistenkaJsonContext : JsonSerializerContext
	{
	}
}
