using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Distenka.Hosting;
using Distenka.Testing;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.Running
{
	public class Running_a_Process
	{
		[Fact]
		public async Task should_run_a_single_item_Process()
		{
			using var runner = new TestHost<ProcessesingleItem>(new Config()).BuildRunner();

			await runner.RunAsync();

			runner.Execution.IsComplete.Should().BeTrue();
			runner.Execution.Disposition.Should().Be(Disposition.Successful);
		}

        [Fact]
        public async Task should_run_a_Process_with_scoped_dependency_in_constructor()
        {
			// CreateDefaultBuilder is more strict with Environment = Dev
			Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

			var signal = new AutoResetEvent(false);

            var action = ProcessorHost.CreateDefaultBuilder(["Process2WithConstructorDep"], typeof(UnitTest).Assembly)
                 .ConfigureServices((context, services) =>
                 {
                     services.AddScoped<IDep1>(s => new Dep1());
					 services.AddSingleton<IDep2>(s => new Dep2());
					 services.AddSingleton(signal);
                 })
                .Build();

			var runAction = action.Services.GetRequiredService<IHostedService>() as RunAction;

			((SignalConfig)runAction.Config).WaitForSignal = true;

			Dep1.IsDisposed.Should().BeFalse();
			
            var run = action.RunAsync();

            Dep1.IsDisposed.Should().BeFalse();

			signal.Set();

            await run;

			Dep1.IsDisposed.Should().BeTrue();
        }
    }
}
