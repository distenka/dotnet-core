using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Distenka.Tests.Scenarios.Packaging
{
	/// <summary>
	/// DCS-027 AC 3 (AD-9): no package the SDK ships contains a Distenka key. Every entry of the packed <c>Distenka</c>,
	/// <c>Distenka.Client</c>, <c>Distenka.Processor.Testing</c> and <c>Distenka.Templates</c> packages is searched — as
	/// bytes/UTF-8 and as UTF-16 (assembly user strings) — for a <c>DK-</c>/<c>DKI-</c> value of key length. Reuses the
	/// packages packed by <see cref="Package_boundaries"/> (<c>./test.ps1</c> builds first).
	/// </summary>
	[Collection(nameof(PackedPackages))]
	public class Package_secret_scan
	{
		/// <summary>A key is the marker plus at least 32 hex digits; the 8-digit public prefix the SDK displays does not match.</summary>
		static readonly Regex KeyShape = new Regex("DKI?-[0-9A-Fa-f]{32,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		readonly Package_boundaries.Packages packages;

		public Package_secret_scan(Package_boundaries.Packages packages) => this.packages = packages;

		[Fact]
		public void no_package_contains_a_key_shaped_string()
		{
			packages.All.Select(p => p.Id).Should().BeEquivalentTo("Distenka", "Distenka.Client", "Distenka.Processor.Testing", "Distenka.Templates");

			var findings = packages.All.SelectMany(p => Findings(p.NupkgPath).Select(f => $"{p.Id}: {f}")).ToList();

			findings.Should().BeEmpty("no Distenka key may be packed (AD-9)");
		}

		[Fact]
		public void the_scan_finds_a_planted_key()
		{
			// Guard: a scan that matches nothing would pass vacuously.
			var nupkg = Path.Combine(Path.GetTempPath(), "distenka-tests", Guid.NewGuid().ToString("N") + ".nupkg");
			Directory.CreateDirectory(Path.GetDirectoryName(nupkg));
			try
			{
				const string planted = "DK-0123456789abcdef0123456789abcdef0123456789abcdef";
				using (var zip = ZipFile.Open(nupkg, ZipArchiveMode.Create))
				{
					Write(zip, "content/appsettings.json", Encoding.UTF8.GetBytes("{ \"Distenka\": { \"ApiKey\": \"" + planted + "\" } }"));
					Write(zip, "lib/net8.0/Job.dll", new byte[] { 0x4D, 0x5A, 0x00 }.Concat(Encoding.Unicode.GetBytes("x" + planted.Replace("DK-", "DKI-"))).ToArray());
					Write(zip, "README.md", Encoding.UTF8.GetBytes("Use AccountKeyCredential; the console shows DK-01234567… only."));
				}

				var findings = Findings(nupkg).ToList();

				findings.Should().HaveCount(2, string.Join(Environment.NewLine, findings));
				findings.Should().Contain(f => f.StartsWith("content/appsettings.json"));
				findings.Should().Contain(f => f.StartsWith("lib/net8.0/Job.dll"));
				findings.Should().NotContain(f => f.Contains("0123456789abcdef0123456789abcdef"), "findings never print the key itself");
			}
			finally
			{
				try { File.Delete(nupkg); } catch { }
			}
		}

		/// <summary>One finding per entry that holds a key-shaped value: the entry name and encoding, never the value.</summary>
		static IEnumerable<string> Findings(string nupkg)
		{
			using var zip = ZipFile.OpenRead(nupkg);
			foreach (var entry in zip.Entries.Where(e => e.Length > 0))
			{
				byte[] bytes;
				using (var stream = entry.Open())
				using (var buffer = new MemoryStream())
				{
					stream.CopyTo(buffer);
					bytes = buffer.ToArray();
				}

				var encodings = new List<string>();
				if (KeyShape.IsMatch(Encoding.Latin1.GetString(bytes)))
					encodings.Add("ASCII/UTF-8");
				if (KeyShape.IsMatch(Encoding.Unicode.GetString(bytes)) || (bytes.Length > 1 && KeyShape.IsMatch(Encoding.Unicode.GetString(bytes, 1, bytes.Length - 1))))
					encodings.Add("UTF-16");

				if (encodings.Count > 0)
					yield return $"{entry.FullName} ({string.Join(", ", encodings)})";
			}
		}

		static void Write(ZipArchive zip, string name, byte[] content)
		{
			using var stream = zip.CreateEntry(name).Open();
			stream.Write(content, 0, content.Length);
		}
	}

	/// <summary>The SDK packages are packed once and shared by the package tests (DCS-010 boundaries, DCS-027 secret scan).</summary>
	[CollectionDefinition(nameof(PackedPackages))]
	public class PackedPackages : ICollectionFixture<Package_boundaries.Packages>
	{
	}
}
