using System;

namespace Distenka
{
	/// <summary>
	/// Distenka API connection information used to report progress and store results of the Processor.
	/// </summary>
	/// <remarks>The data in this class is used by the Distenka API, node and/or Processor host. This class should not be extended.</remarks>
	public class DistenkaApiConfig
	{
		/// <summary>
		/// The URI of the Distenka API, https://api.distenka.ai/ by default.
		/// </summary>
		public string Uri { get; set; }
		
		/// <summary>
		/// When true, successful item results will be sent to the API. This is similar to turning on debug logging, it may severely impact the performance of Processes.
		/// </summary>
		public bool LogSuccessfulItemResults { get; set; }

		/// <summary>
		/// The org that the run is queued or executing in.
		/// </summary>
		public string OrganizationId { get; set; }
		
		/// <summary>
		/// The ID of the instance assigned by the API.
		/// </summary>
		public Guid InstanceId { get; set; }
		
		/// <summary>
		/// Initializes a new <see cref="DistenkaApiConfig"/>.
		/// </summary>
		public DistenkaApiConfig()
		{
			Uri = "https://api.distenka.ai/";
		}
	}
}
