﻿using System;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;
using Distenka.Hosting;

namespace Distenka.Tests.Scenarios.ServiceCollectionConfiguration
{
	public class Discovering_a_Process : UnitTest
	{
		readonly ServiceProvider sp;
		readonly ProcessCache processCache;

		readonly Type[] deps = new Type[]
		{
			typeof(Dep1),
			typeof(Dep2),
			typeof(Dep3),
			typeof(Dep4),
			typeof(Dep5),
			typeof(Dep6),
			typeof(Dep7),
			typeof(Dep8),
			typeof(Dep9),
			typeof(Dep10),
			typeof(Dep11),
			typeof(Dep12),
			typeof(Dep13),
			typeof(Dep14),
			typeof(Dep15),
			typeof(Dep16)
		};

		public Discovering_a_Process()
		{
			var services = new ServiceCollection();
			services.AddDistenkaProcessor(new[] { "list" }, typeof(UnitTest).Assembly);
			sp = services.BuildServiceProvider();

			processCache = sp.GetRequiredService<ProcessCache>();
		}

		public override void Dispose()
		{
			sp?.Dispose();
		}

		[Fact]
		public void Should_load_AsyncProcessConfOnly()
		{
			var type = typeof(ProcessesingleItem);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().BeNull();
			pi.Dependencies.Should().BeEmpty();
		}

		[Fact]
		public void Should_load_AsyncProcess0()
		{
			var type = typeof(Process0);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().BeEmpty();
		}

		[Fact]
		public void Should_load_AsyncProcess1()
		{
			var type = typeof(Process1);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(1);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 1));
		}

		[Fact]
		public void Should_load_AsyncProcess2()
		{
			var type = typeof(Process2);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(2);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 2));
		}

		[Fact]
		public void Should_load_AsyncProcess3()
		{
			var type = typeof(Process3);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(3);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 3));
		}

		[Fact]
		public void Should_load_AsyncProcess4()
		{
			var type = typeof(Process4);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(4);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 4));
		}

		[Fact]
		public void Should_load_AsyncProcess5()
		{
			var type = typeof(Process5);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(5);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 5));
		}

		[Fact]
		public void Should_load_AsyncProcess6()
		{
			var type = typeof(Process6);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(6);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 6));
		}

		[Fact]
		public void Should_load_AsyncProcess7()
		{
			var type = typeof(Process7);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(7);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 7));
		}

		[Fact]
		public void Should_load_AsyncProcess8()
		{
			var type = typeof(Process8);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(8);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 8));
		}

		[Fact]
		public void Should_load_AsyncProcess9()
		{
			var type = typeof(Process9);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(9);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 9));
		}

		[Fact]
		public void Should_load_AsyncProcess10()
		{
			var type = typeof(Process10);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(10);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 10));
		}

		[Fact]
		public void Should_load_AsyncProcess11()
		{
			var type = typeof(Process11);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(11);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 11));
		}

		[Fact]
		public void Should_load_AsyncProcess12()
		{
			var type = typeof(Process12);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(12);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 12));
		}

		[Fact]
		public void Should_load_AsyncProcess13()
		{
			var type = typeof(Process13);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(13);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 13));
		}

		[Fact]
		public void Should_load_AsyncProcess14()
		{
			var type = typeof(Process14);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(14);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 14));
		}

		[Fact]
		public void Should_load_AsyncProcess15()
		{
			var type = typeof(Process15);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(15);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 15));
		}

		[Fact]
		public void Should_load_AsyncProcess16()
		{
			var type = typeof(Process16);
			var pi = processCache.Get(type);

			pi.Should().NotBeNull();
			pi.ProcessType.Should().Be(type);
			pi.ItemType.Should().Be(typeof(int));
			pi.Dependencies.Should().HaveCount(16);
			pi.Dependencies.Should().BeEquivalentTo(Subset(deps, 16));
		}

		private T[] Subset<T>(T[] array, int length)
		{
			var subset = new T[length];

			Array.Copy(array, 0, subset, 0, length);

			return subset;
		}
	}
}
