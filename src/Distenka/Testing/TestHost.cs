using Microsoft.Extensions.DependencyInjection;
using System;

namespace Distenka.Testing
{
	/// <summary>
	/// A program initialization abstraction for testing Distenka Processes.
	/// </summary>
	/// <typeparam name="TProcess">The type of <see cref="IProcessor"/> being tested.</typeparam>
	public class TestHost<TProcess>
		where TProcess : IProcessor
	{
		readonly Config config;
		Action<IServiceCollection> configureDelegate;

		/// <summary>
		/// Initializes the <see cref="TestHost{TProcess}"/>.
		/// </summary>
		/// <param name="config">The config for the <see cref="IProcessor"/> to run.</param>
		public TestHost(Config config)
		{
			this.config = config ?? throw new ArgumentNullException(nameof(config));

			if (string.IsNullOrWhiteSpace(config.Process.Type))
				config.Process.Type = typeof(TProcess).FullName;
		}

		/// <summary>
		/// Adds services to the container. This can be called multiple times and the results will be additive.
		/// </summary>
		/// <param name="configureDelegate">The delegate for configuring the <see cref="IServiceCollection"/> that will be used to construct the <see cref="IServiceProvider"/>.</param>
		/// <returns>The same instance of the <see cref="TestHost{TProcess}"/> for chaining.</returns>
		public TestHost<TProcess> ConfigureServices(Action<IServiceCollection> configureDelegate)
		{
			this.configureDelegate = configureDelegate;
			return this;
		}

		/// <summary>
		/// Creates a new <see cref="TestRun{TProcess}"/>.
		/// </summary>
		public TestRun<TProcess> BuildRunner()
		{
			var services = new ServiceCollection();
			services.AddDistenkaProcessor(config, typeof(TProcess).Assembly);
			services.AddLogging();

			if (configureDelegate != null)
				configureDelegate(services);

			var provider = services.BuildServiceProvider();
			var execution = provider.GetRequiredService<Execution>();
			return new TestRun<TProcess>((TProcess)execution.Processor, execution, new ResultLog(execution), provider);
		}
	}
}
