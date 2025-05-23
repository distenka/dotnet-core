﻿using System;

namespace Distenka
{
	/// <summary>
	/// Determines how a Processor will execute and update status.
	/// </summary>
	/// <remarks>The data in this class is used by the Distenka API, Processor host. This class should not be extended.</remarks>
	public class ExecutionConfig
	{
		/// <summary>
		/// your Org name.
		/// </summary>
		public string OrgName { get; set; }
		
		/// <summary>
		/// The API key on distenka platform.
		/// </summary>
		public string APIKey { get; set; }

		/// <summary>
		/// The Environment name.
		/// </summary>
		public string Env { get; set; }
		
		/// <summary>
		/// The Processor Name.
		/// </summary>
		public string Processor { get; set; }
		
		/// <summary>
		/// The current Run Id item.
		/// </summary>
		public string RunId { get; set; }
		
		/// <summary>
		/// The number of tasks created to call in parallel the ProcessAsync method of the Processor.
		/// </summary>
		/// <value>1 by default</value>
		public int ParallelTaskCount { get; set; }

		/// <summary>
		/// The number of times, after the first attempt, to reprocess an item when an unhandled exception occurs or a failed result is returned.
		/// </summary>
		/// <value>0 by default</value>
		public int ItemFailureRetryCount { get; set; }

		/// <summary>
		/// The number of items which must fail before the Processor is stopped, ending in a failure state.
		/// </summary>
		/// <value>1 by default</value>
		/// <remarks>When a Processor is executed using parallel tasks, the results for the Processor may indicate more items failed than is specified in this property because items are allowed to complete when the Processor is failing. For example, if <see cref="ItemFailureCountToStopProcess"/> is set to 1 and <see cref="ParallelTaskCount"/> is set to 5 and the Processor tries to connect to a mis-typed URL, the first task that fails will trigger a Processor failure but the remaining four tasks will run to completion, ultimately resulting in five failed items.</remarks>
		public int ItemFailureCountToStopProcess { get; set; }

		/// <summary>
		/// When true, will catch exceptions that occur within the Processor and write the exception to the results and/or API. 
		/// </summary>
		/// <remarks>This setting is true by default and should only be set to false for debugging purposes. Setting this to false will cause the application to stop when an exception occurs and FinalizeAsync is not guaranteed to execute.</remarks>
		/// <value>true by default</value>
		public bool HandleExceptions { get; set; }

		/// <summary>
		/// When true, writes the results to the path specified in <see cref="ResultsFilePath"/>.
		/// </summary>
		/// <remarks>If <see cref="ResultsFilePath"/> is not specified the results are written to a file named results.json in the same location as the config file.</remarks>
		/// <value>false by default</value>
		public bool ResultsToFile { get; set; }

		/// <summary>
		/// The file path to write the results to. Used only when <see cref="ResultsToFile"/> is true.
		/// </summary>
		/// <remarks>This path is relative to the directory containing the config file.</remarks>
		public string ResultsFilePath { get; set; }

		/// <summary>
		/// When true, writes the results to the console (stdout).
		/// </summary>
		/// <value>false by default</value>
		public bool ResultsToConsole { get; set; }

		/// <summary>
		/// When true, causes the Processor to prompt the user to attach a debugger.
		/// </summary>
		/// <remarks>Ignored when the Processor does not run in an interactive session.</remarks>
		public bool LaunchDebugger { get; set; }

		/// <summary>
		/// The amount of time, in seconds, to wait before allowing the Processor to execute after being queued.
		/// </summary>
		/// <remarks>This setting is used by the Distenka API to delay the assignment of the run to a node. It has no effect when running locally.</remarks>
		public int Delay { get; set; }

		/// <summary>
		/// Blocks the execution of the Processor until the Processor identified by the Run ID has completed.
		/// </summary>
		/// <remarks>This setting is used by the Distenka API to block the assignment of the run to a node until the other run has completed. It has no effect when running locally.</remarks>
		public Guid? RunAfterId { get; set; }

		/// <summary>
		/// Initializes a new instance of <see cref="ExecutionConfig"/>
		/// </summary>
		public ExecutionConfig()
		{
			OrgName = string.Empty;
			APIKey = string.Empty;
			Env = string.Empty;
			Processor = string.Empty;
			RunId = string.Empty;
			ParallelTaskCount = 1;
			ItemFailureRetryCount = 0;
			ItemFailureCountToStopProcess = 1;
			HandleExceptions = true;
			ResultsToConsole = true;
			ResultsToFile = false;
			ResultsFilePath = string.Empty;
		}
	}
}
