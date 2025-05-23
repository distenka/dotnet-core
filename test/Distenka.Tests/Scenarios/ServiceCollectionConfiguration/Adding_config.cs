﻿using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Tests.Scenarios.ServiceCollectionConfiguration
{
	public interface InterfaceConfig { }
	public abstract class AbstractConfig : Config, InterfaceConfig { }
	public class ConcreteExtendedConfig : AbstractConfig { }
	public class ConcreteConfig : ConcreteExtendedConfig { }

	public class ConfigTestProcessor : Processor<ConcreteConfig>
	{
		public ConfigTestProcessor(ConcreteConfig config) : base(config) { }

		public override Task<Result> ProcessAsync() => throw new NotImplementedException();
	}

	public class Adding_config : UnitTest
	{
		readonly ServiceProvider sp;

		public Adding_config()
		{
			var config = new ConcreteConfig();
			config.Process.Type = typeof(ConfigTestProcessor).FullName;

			var services = new ServiceCollection();
			services.AddDistenkaProcessor(config, typeof(UnitTest).Assembly);
			sp = services.BuildServiceProvider();
		}

		public override void Dispose()
		{
			sp?.Dispose();
		}

		[Fact]
		public void Should_register_base_types()
		{
			sp.GetRequiredService<Config>().Should().NotBeNull();
			sp.GetRequiredService<InterfaceConfig>().Should().NotBeNull();
			sp.GetRequiredService<AbstractConfig>().Should().NotBeNull();
			sp.GetRequiredService<ConcreteExtendedConfig>().Should().NotBeNull();
			sp.GetRequiredService<ConcreteConfig>().Should().NotBeNull();
		}
	}
}