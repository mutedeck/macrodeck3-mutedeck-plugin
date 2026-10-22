using System.IO.Compression;
using System.Text.Json;
using MuteDeckPlugin.Actions;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

/// <summary>Guards what the plugin addresses by name against what it actually ships.</summary>
[TestFixture]
public sealed class PackagingTests
{
	private static readonly string _projectDirectory = Path.Combine(FindRepositoryRoot(), "src", "MuteDeckPlugin");

	[Test]
	public void Every_icon_the_actions_use_is_in_the_bundled_pack()
	{
		var names = IconNames();
		var used = typeof(MuteDeckIcons)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(field => field.IsLiteral && field.Name != nameof(MuteDeckIcons.PackKey))
			.Select(field => (string)field.GetRawConstantValue()!);

		Assert.That(names, Is.SupersetOf(used));
		Assert.That(names, Is.SupersetOf(MuteDeckIcons.CustomActionIcons));
	}

	[Test]
	public void The_manifest_bundles_the_pack_under_the_key_the_actions_use()
	{
		using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_projectDirectory, "manifest.json")));

		var pack = manifest.RootElement.GetProperty("bundledIconPacks").EnumerateArray().Single();

		Assert.That(pack.GetProperty("key").GetString(), Is.EqualTo(MuteDeckIcons.PackKey));
		Assert.That(File.Exists(Path.Combine(_projectDirectory, pack.GetProperty("path").GetString()!)), Is.True);
	}

	[Test]
	public void The_manifest_declares_only_the_supported_platforms()
	{
		using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_projectDirectory, "manifest.json")));

		var platforms = manifest.RootElement.GetProperty("entrypoints").EnumerateObject().Select(entry => entry.Name);

		Assert.That(platforms, Is.EquivalentTo(new[] { "osx-arm64", "win-x64" }));
	}

	private static HashSet<string> IconNames()
	{
		using var archive = ZipFile.OpenRead(Path.Combine(_projectDirectory, "icon-packs", "mutedeck.macroDeckIconPack"));
		using var stream = archive.GetEntry("pack.json")!.Open();
		using var pack = JsonDocument.Parse(stream);
		return pack.RootElement.GetProperty("icons").EnumerateArray()
			.Select(icon => icon.GetProperty("name").GetString()!)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MuteDeckPlugin.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
	}
}
