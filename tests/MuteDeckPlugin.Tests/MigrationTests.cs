using System.Text.Json;
using MacroDeck.Sdk.Migration;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class MigrationTests
{
	private const string Namespace = "MuteDeck.Plugin.Actions.";

	[Test]
	public async Task The_macro_deck_2_plugin_is_claimed()
	{
		await using var plugin = TestPlugin.Create();

		var migration = plugin.Integration.Migrations.Single();

		Assert.That(migration.Source, Is.EqualTo(MigrationSource.MacroDeck2));
		Assert.That(migration.ClaimedActionSources, Is.EqualTo(new[] { "MuteDeckPlugin" }));
		Assert.That(migration.ClaimedSettingsSources, Does.Contain("mutedeck_mutedeck"));
	}

	[TestCase("ToggleMuteAction", "toggle-mute")]
	[TestCase("ToggleVideoAction", "toggle-video")]
	[TestCase("ToggleShareAction", "toggle-share")]
	[TestCase("ToggleRecordAction", "toggle-record")]
	public async Task A_toggle_keeps_its_state(string typeName, string actionId)
	{
		var result = await Migrate(typeName, """{"State":"off"}""", "My button");

		Assert.That(result!.IntegrationId, Is.EqualTo(TestPlugin.PluginId));
		Assert.That(result.ActionId, Is.EqualTo(actionId));
		Assert.That(result.Label, Is.EqualTo("My button"));
		Assert.That(result.Parameters["state"].GetString(), Is.EqualTo("off"));
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("{}")]
	public async Task A_toggle_without_configuration_toggles(string? configuration)
	{
		var result = await Migrate("ToggleMuteAction", configuration);

		Assert.That(result!.Parameters["state"].GetString(), Is.EqualTo("toggle"));
		Assert.That(result.Label, Is.EqualTo("Microphone: mute/unmute"));
	}

	[TestCase("""{"State":"sideways"}""")]
	[TestCase("not json")]
	public async Task An_unreadable_toggle_stays_a_placeholder(string configuration)
	{
		Assert.That(await Migrate("ToggleMuteAction", configuration), Is.Null);
	}

	[TestCase("LeaveMeetingAction", "leave-meeting")]
	[TestCase("BringToFrontAction", "bring-to-front")]
	public async Task Actions_without_settings_map_directly(string typeName, string actionId)
	{
		var result = await Migrate(typeName, null);

		Assert.That(result!.ActionId, Is.EqualTo(actionId));
		Assert.That(result.Parameters, Is.Empty);
	}

	[Test]
	public async Task A_custom_action_keeps_its_name()
	{
		var result = await Migrate("CustomActionAction", """{"Name":"raise-hand"}""");

		Assert.That(result!.ActionId, Is.EqualTo("custom-action"));
		Assert.That(result.Parameters["name"].GetString(), Is.EqualTo("raise-hand"));
	}

	[Test]
	public async Task A_custom_action_without_a_name_stays_a_placeholder()
	{
		Assert.That(await Migrate("CustomActionAction", """{"Name":""}"""), Is.Null);
	}

	[Test]
	public async Task Another_type_is_not_translated()
	{
		Assert.That(await Migrate("SomethingElse", null), Is.Null);
	}

	[Test]
	public async Task Settings_and_the_approval_token_are_carried_over()
	{
		await using var plugin = TestPlugin.Create();
		var migration = plugin.Integration.Migrations.Single();

		var configurations = await migration.MigrateConfigurationAsync(new ForeignPluginSettings("mutedeck_mutedeck",
				new Dictionary<string, string> { ["host"] = "studio-pc", ["port"] = "4000" },
				[new Dictionary<string, string> { ["mutedeck_token"] = "token-1", ["mutedeck_instance_id"] = "instance-1" }],
				[]),
			CancellationToken.None);

		var configuration = configurations.Single();
		Assert.That(configuration.IntegrationId, Is.EqualTo(TestPlugin.PluginId));
		Assert.That(configuration.Values["host"].GetString(), Is.EqualTo("studio-pc"));
		Assert.That(configuration.Values["port"].GetString(), Is.EqualTo("4000"));
		Assert.That(configuration.Secrets["mutedeck_token"], Is.EqualTo(new MigratedSecret("token-1", MigratedSecretKind.Secret)));
		Assert.That(configuration.Secrets["mutedeck_instance_id"].Value, Is.EqualTo("instance-1"));
	}

	[Test]
	public async Task Without_settings_the_defaults_are_migrated()
	{
		await using var plugin = TestPlugin.Create();
		var migration = plugin.Integration.Migrations.Single();

		var configuration = (await migration.MigrateConfigurationAsync(
			new ForeignPluginSettings("mutedeck_mutedeck", new Dictionary<string, string>(), [], []),
			CancellationToken.None)).Single();

		Assert.That(configuration.Values["host"].GetString(), Is.EqualTo("localhost"));
		Assert.That(configuration.Values["port"].GetString(), Is.EqualTo("3492"));
		Assert.That(configuration.Secrets, Is.Empty);
	}

	private static async Task<ActionMigrationResult?> Migrate(string typeName, string? configuration, string? displayName = null)
	{
		await using var plugin = TestPlugin.Create();
		return await plugin.Integration.Migrations.Single().MigrateActionAsync(
			new ForeignAction(Namespace + typeName, "MuteDeckPlugin", displayName, configuration, null),
			CancellationToken.None);
	}
}
