using Newtonsoft.Json;
using NJsonSchema.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Distenka
{
	/// <summary>
	/// The base config class used to configure Processes.
	/// </summary>
	/// <remarks>Public members of this class and classes that extend it must be JSON serializable.</remarks>
	public class Config
	{
		[JsonIgnore]
		internal string __filePath;

		/// <summary>
		/// Identifies the Processor that created this config and/or the Processor that will run when executed.
		/// </summary>
		[Required]
		[JsonConverter(typeof(ProcessConfigConverter))]
		public ProcessorConfig Process { get; set; }

		/// <summary>
		/// Determines how the Processor will execute.
		/// </summary>
		[NotNull]
		public ExecutionConfig Execution { get; set; }

		/// <summary>
		/// Distenka API connection information used to report progress and store results of the Processor.
		/// </summary>
		[JsonSchemaIgnore]
		public DistenkaApiConfig DistenkaApi { get; set; }

		/// <summary>
		/// Initializes a new <see cref="Config"/>.
		/// </summary>
		public Config()
		{
			Process = new ProcessorConfig();
			Execution = new ExecutionConfig();
			DistenkaApi = new DistenkaApiConfig();
		}
	}
}
