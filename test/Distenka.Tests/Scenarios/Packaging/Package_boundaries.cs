using Distenka.Testing;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Distenka.Tests.Scenarios.Packaging
{
	/// <summary>
	/// DCS-010: the package boundary of ADR-001 §2.1 / ADR-005 §2.1.3 / ADR-008 §2.1.1, asserted on the .nuspec files that
	/// `dotnet pack` actually generates (not on the csproj text). Packs the already-built projects with <c>--no-build</c>,
	/// so the solution must be built first in the same configuration (<c>./test.ps1</c> does).
	/// </summary>
	[Collection(nameof(PackedPackages))]
	public class Package_boundaries
	{
		/// <summary>ADR-001 §2.1 and ADR-007 §2.2: prohibited for every SDK package, without exception (prefix match, case-insensitive).</summary>
		static readonly string[] ProhibitedEverywhere =
		{
			"Microsoft.Orleans.", "StackExchange.Redis", "Npgsql", "Microsoft.EntityFrameworkCore", "KubernetesClient",
			"Azure.", "Microsoft.Azure.", "RabbitMQ.", "MassTransit", "Distenka.Core.",
			"Microsoft.AspNetCore.SignalR" // ADR-007 §2.2: no SignalR client in any SDK package
		};

		/// <summary>ADR-008 §2.1.1 / DCS-010 AC 3: never a dependency of Distenka.Client (exact id, case-insensitive).</summary>
		static readonly string[] ProhibitedInClient =
		{
			"Distenka", "Distenka.Processor", "Distenka.Processor.Testing", "Newtonsoft.Json", "NJsonSchema", "Spectre.Console",
			"System.CommandLine", "Microsoft.AspNetCore.SignalR.Client"
		};

		/// <summary>ADR-001 §2.1: the testing package must work under every test framework.</summary>
		static readonly string[] TestFrameworks =
		{
			"xunit", "xunit.core", "xunit.assert", "xunit.abstractions", "NUnit", "MSTest.TestFramework", "MSTest.TestAdapter",
			"Microsoft.NET.Test.Sdk", "FluentAssertions"
		};

		readonly Packages packages;

		public Package_boundaries(Packages packages) => this.packages = packages;

		// ---- Distenka (processor host) ---------------------------------------------------------------------------------

		[Fact]
		public void processor_package_keeps_id_Distenka_and_ships_Distenka_dll()
		{
			// AC 2 / AD-12: existing job packages keep working.
			var p = packages.Processor;
			p.Id.Should().Be("Distenka");
			p.Files.Should().Contain("lib/net8.0/Distenka.dll");
			p.Files.Where(f => f.StartsWith("lib/", StringComparison.Ordinal) && f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				.Should().Equal(new[] { "lib/net8.0/Distenka.dll" }, "the host is one assembly for one target framework (ADR-005 §2.1)");
		}

		[Fact]
		public void processor_package_has_no_client_test_helper_or_platform_dependency()
		{
			var p = packages.Processor;
			p.DependencyGroups.Keys.Should().Equal(new[] { "net8.0" });
			p.AllDependencyIds.Should().NotContain(id => Is(id, "Distenka.Client") || Is(id, "Distenka.Processor.Testing") || Is(id, "Microsoft.AspNetCore.SignalR.Client"));
			p.AllDependencyIds.Should().NotContain(id => ProhibitedEverywhere.Any(x => id.StartsWith(x, StringComparison.OrdinalIgnoreCase)));
		}

		[Fact]
		public void processor_package_needs_no_shared_framework_beyond_the_runtime()
		{
			// ADR-005 §2.1.3: the executor image is runtime-only (mcr.microsoft.com/dotnet/runtime:8.0).
			packages.Processor.FrameworkReferences.Should().NotContain(f =>
				f.StartsWith("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase) ||
				f.StartsWith("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void test_helpers_are_not_in_the_processor_assembly()
		{
			typeof(Execution).Assembly.GetName().Name.Should().Be("Distenka");
			typeof(Execution).Assembly.GetType("Distenka.Testing.TestHost`1").Should().BeNull("TestHost moved to Distenka.Processor.Testing");
			typeof(Execution).Assembly.GetType("Distenka.Testing.TestRun`1").Should().BeNull("TestRun moved to Distenka.Processor.Testing");
			typeof(TestHost<>).Assembly.GetName().Name.Should().Be("Distenka.Processor.Testing");
			typeof(TestHost<>).Namespace.Should().Be("Distenka.Testing", "the namespace is unchanged (ADR-001 §2.4)");
		}

		// ---- Distenka.Client --------------------------------------------------------------------------------------------

		[Fact]
		public void client_package_targets_net8_and_netstandard2()
		{
			var c = packages.Client;
			c.Id.Should().Be("Distenka.Client");
			c.Files.Should().Contain(new[] { "lib/net8.0/Distenka.Client.dll", "lib/netstandard2.0/Distenka.Client.dll" });
			c.DependencyGroups.Keys.Should().BeEquivalentTo(new[] { "net8.0", ".NETStandard2.0" });
		}

		[Fact]
		public void client_package_never_depends_on_the_host_stack()
		{
			// AC 3 (and DCS-008's package-dependency check): no ProjectReference to the host, no Newtonsoft, Spectre,
			// System.CommandLine, NJsonSchema or SignalR client.
			var ids = packages.Client.AllDependencyIds;
			ids.Should().NotContain(id => ProhibitedInClient.Any(x => Is(id, x)));
			ids.Should().NotContain(id => ProhibitedEverywhere.Any(x => id.StartsWith(x, StringComparison.OrdinalIgnoreCase)));
		}

		[Fact]
		public void client_uses_the_System_Text_Json_package_on_netstandard2_only()
		{
			// ADR-008 §2.1.2: >= 8.0.5 (CVE-2024-43485) on netstandard2.0; in-box on net8.0.
			var groups = packages.Client.DependencyGroups;
			groups["net8.0"].Should().NotContain(d => Is(d.Id, "System.Text.Json"));

			var stj = groups[".NETStandard2.0"].Where(d => Is(d.Id, "System.Text.Json")).ToList();
			stj.Should().ContainSingle();
			MinimumVersion(stj[0].Version).Should().BeGreaterThanOrEqualTo(new Version(8, 0, 5));
		}

		// ---- Distenka.Processor.Testing ---------------------------------------------------------------------------------

		[Fact]
		public void testing_package_depends_on_the_host_only()
		{
			var t = packages.Testing;
			t.Id.Should().Be("Distenka.Processor.Testing");
			t.Files.Should().Contain("lib/net8.0/Distenka.Processor.Testing.dll");
			t.DependencyGroups.Keys.Should().Equal(new[] { "net8.0" });

			var host = t.DependencyGroups["net8.0"].Where(d => Is(d.Id, "Distenka")).ToList();
			host.Should().ContainSingle("the testing package builds on the processor host");
			host[0].Version.Should().Contain(packages.Processor.Version, "both packages are released together");

			t.AllDependencyIds.Should().NotContain(id => Is(id, "Distenka.Client"));
			t.AllDependencyIds.Should().NotContain(id => TestFrameworks.Any(x => Is(id, x)), "it must work under xUnit, NUnit and MSTest");
			t.AllDependencyIds.Should().NotContain(id => ProhibitedEverywhere.Any(x => id.StartsWith(x, StringComparison.OrdinalIgnoreCase)));
		}

		static bool Is(string id, string expected) => string.Equals(id, expected, StringComparison.OrdinalIgnoreCase);

		/// <summary>Lower bound of a NuGet version range: "8.0.5", "[8.0.5, )", "[8.0.5]".</summary>
		static Version MinimumVersion(string range)
		{
			var min = range.Trim().TrimStart('[', '(').Split(',')[0].TrimEnd(']', ')').Trim();
			var release = min.Split('-')[0];
			return Version.Parse(release);
		}

		// ---- Fixture ----------------------------------------------------------------------------------------------------

		/// <summary>Packs the three SDK projects once per test run.</summary>
		public sealed class Packages : IDisposable
		{
			readonly string output = Path.Combine(Path.GetTempPath(), "distenka-tests", "packages-" + Guid.NewGuid().ToString("N"));

			public Nuspec Processor { get; }
			public Nuspec Client { get; }
			public Nuspec Testing { get; }

			/// <summary>The template/sample package (DCS-027: scanned for secrets).</summary>
			public Nuspec Templates { get; }

			/// <summary>Every package packed here.</summary>
			public IReadOnlyList<Nuspec> All => new[] { Processor, Client, Testing, Templates };

			public Packages()
			{
				var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				var configuration = baseDir.Parent.Name; // bin/<Configuration>/net8.0

				var root = baseDir;
				while (root != null && !File.Exists(Path.Combine(root.FullName, "Distenka.sln")))
					root = root.Parent;
				if (root == null)
					throw new InvalidOperationException("The package tests run from inside the repository.");

				Directory.CreateDirectory(output);
				Processor = Pack(root.FullName, configuration, Path.Combine("src", "Distenka.Processor", "Distenka.Processor.csproj"), "Distenka");
				Client = Pack(root.FullName, configuration, Path.Combine("src", "Distenka.Client", "Distenka.Client.csproj"), "Distenka.Client");
				Testing = Pack(root.FullName, configuration, Path.Combine("src", "Distenka.Processor.Testing", "Distenka.Processor.Testing.csproj"), "Distenka.Processor.Testing");
				Templates = Pack(root.FullName, configuration, Path.Combine("src", "Templates", "Templates.csproj"), "Distenka.Templates");
			}

			Nuspec Pack(string root, string configuration, string project, string packageId)
			{
				var dir = Path.Combine(output, packageId);
				Directory.CreateDirectory(dir);

				var psi = new ProcessStartInfo(ProcessorProcess.DotnetHost())
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = root, // global.json applies
				};
				foreach (var a in new[] { "pack", project, "-c", configuration, "--no-build", "-o", dir, "--nologo" })
					psi.ArgumentList.Add(a);

				// `dotnet test` exports MSBuild locations into the test host's environment; let the child CLI resolve its own.
				foreach (var v in new[] { "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildLoadMicrosoftTargetsReadOnly" })
					psi.Environment.Remove(v);
				psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
				psi.Environment["DOTNET_NOLOGO"] = "1";

				using var p = System.Diagnostics.Process.Start(psi);
				var stdout = p.StandardOutput.ReadToEndAsync();
				var stderr = p.StandardError.ReadToEndAsync();
				if (!p.WaitForExit(180_000))
				{
					try { p.Kill(entireProcessTree: true); } catch { }
					throw new TimeoutException($"dotnet pack {project} did not finish in 180 s.");
				}
				p.WaitForExit();

				var nupkg = Directory.EnumerateFiles(dir, packageId + ".*.nupkg")
					.Where(f => !f.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
					.SingleOrDefault();

				if (p.ExitCode != 0 || nupkg == null)
					throw new InvalidOperationException(
						$"dotnet pack {project} -c {configuration} --no-build failed (exit {p.ExitCode}). Build Distenka.sln in {configuration} first (./test.ps1 does)." +
						$"{Environment.NewLine}{stdout.Result}{Environment.NewLine}{stderr.Result}");

				return Nuspec.Read(nupkg);
			}

			public void Dispose()
			{
				try { Directory.Delete(output, recursive: true); } catch { }
			}
		}

		/// <summary>The parts of a generated .nuspec the boundary tests look at.</summary>
		public sealed class Nuspec
		{
			public string Id { get; private set; }
			public string Version { get; private set; }
			/// <summary>The .nupkg file (deleted with the fixture).</summary>
			public string NupkgPath { get; private set; }
			public IReadOnlyList<string> Files { get; private set; }
			public IReadOnlyDictionary<string, IReadOnlyList<(string Id, string Version)>> DependencyGroups { get; private set; }
			public IReadOnlyList<string> FrameworkReferences { get; private set; }
			public IEnumerable<string> AllDependencyIds => DependencyGroups.Values.SelectMany(g => g.Select(d => d.Id));

			public static Nuspec Read(string nupkg)
			{
				using var zip = ZipFile.OpenRead(nupkg);
				var entry = zip.Entries.Single(e => !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

				XDocument doc;
				using (var s = entry.Open())
					doc = XDocument.Load(s);

				var metadata = doc.Root.Elements().Single(e => e.Name.LocalName == "metadata");
				string Value(string name) => metadata.Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;

				var dependencies = metadata.Elements().SingleOrDefault(e => e.Name.LocalName == "dependencies");
				var groups = new Dictionary<string, IReadOnlyList<(string, string)>>(StringComparer.OrdinalIgnoreCase);
				if (dependencies != null)
				{
					foreach (var g in dependencies.Elements().Where(e => e.Name.LocalName == "group"))
						groups[(string)g.Attribute("targetFramework") ?? ""] = g.Elements().Where(e => e.Name.LocalName == "dependency")
							.Select(d => ((string)d.Attribute("id"), (string)d.Attribute("version"))).ToList();

					var ungrouped = dependencies.Elements().Where(e => e.Name.LocalName == "dependency").ToList();
					if (ungrouped.Count > 0)
						groups[""] = ungrouped.Select(d => ((string)d.Attribute("id"), (string)d.Attribute("version"))).ToList();
				}

				var frameworkReferences = metadata.Descendants().Where(e => e.Name.LocalName == "frameworkReference")
					.Select(e => (string)e.Attribute("name")).ToList();

				return new Nuspec
				{
					NupkgPath = nupkg,
					Id = Value("id"),
					Version = Value("version"),
					Files = zip.Entries.Select(e => e.FullName).ToList(),
					DependencyGroups = groups,
					FrameworkReferences = frameworkReferences,
				};
			}
		}
	}
}
