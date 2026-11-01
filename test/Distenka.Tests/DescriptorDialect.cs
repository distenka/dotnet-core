using System.Collections.Generic;
using System.Linq;
using Distenka.Testing;

namespace Distenka.Tests
{
	/// <summary>
	/// The platform's descriptor-dialect rules and the executor's <c>ExtractJsonObject</c>, as used by the DCS-043 tests.
	/// Since DCS-093 a thin adapter over the shipped <see cref="DescriptorSchemaValidator"/> and
	/// <see cref="Distenka.Testing.ExecutorRules"/> (Distenka.Processor.Testing), so the rules live in one place.
	/// </summary>
	static class DescriptorDialect
	{
		public static IReadOnlyList<string> Keywords => DescriptorSchemaValidator.Keywords;

		/// <summary>Returns the reasons the platform would call the schema unusable (empty = usable).</summary>
		public static List<string> Validate(string json, int maxDepth = 16) =>
			DescriptorSchemaValidator.Validate(json, maxDepth).ToList();

		/// <summary>The executor's <c>ExtractJsonObject</c>: first '{' to its balancing '}' by raw brace counting.</summary>
		public static string ExtractJsonObject(string stdout) => Distenka.Testing.ExecutorRules.ExtractJsonObject(stdout);
	}
}
