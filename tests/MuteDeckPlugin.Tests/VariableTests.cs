using MacroDeck.Sdk.Variables;
using MuteDeck.PluginClient;
using MuteDeckPlugin.Connection;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class VariableTests
{
	private static readonly string[] _booleans =
	[
		"mutedeck_connected", "mutedeck_in_call", "mutedeck_muted", "mutedeck_video_active", "mutedeck_share_active",
		"mutedeck_record_active"
	];

	private static readonly string[] _texts =
	[
		"mutedeck_call_status", "mutedeck_mute_status", "mutedeck_video_status", "mutedeck_share_status",
		"mutedeck_record_status", "mutedeck_control_status", "mutedeck_call_label", "mutedeck_mute_label",
		"mutedeck_video_label", "mutedeck_share_label", "mutedeck_record_label"
	];

	[Test]
	public async Task The_macro_deck_2_variable_names_are_kept_exactly()
	{
		await using var plugin = TestPlugin.Create();

		var variables = plugin.Integration.Variables;

		Assert.That(variables.Select(variable => variable.Name), Is.EquivalentTo(_booleans.Concat(_texts)));
		Assert.That(variables.Where(variable => variable.Type == VariableType.Boolean).Select(variable => variable.Name),
			Is.EquivalentTo(_booleans));
		Assert.That(variables.All(variable => variable.Id == variable.Name!.Replace('_', '-')), Is.True);
		Assert.That(variables.All(variable => !variable.DisplayName.IsEmpty), Is.True);
	}

	[Test]
	public async Task While_disconnected_only_connected_has_a_value()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		Assert.That(await Read(plugin, "mutedeck-connected"), Is.EqualTo(false));
		foreach (var name in _booleans.Skip(1).Concat(_texts))
		{
			Assert.That((await plugin.Integration.ReadAsync(name.Replace('_', '-'))).Value, Is.Null, name);
		}
	}

	[Test]
	public async Task Connected_values_follow_muteDeck()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect(new MuteDeckStatus("active", "active", "inactive", "disabled", "", "teams"));

		Assert.That(await Read(plugin, "mutedeck-connected"), Is.EqualTo(true));
		Assert.That(await Read(plugin, "mutedeck-in-call"), Is.EqualTo(true));
		Assert.That(await Read(plugin, "mutedeck-muted"), Is.EqualTo(true));
		Assert.That(await Read(plugin, "mutedeck-video-active"), Is.EqualTo(false));
		Assert.That(await Read(plugin, "mutedeck-mute-status"), Is.EqualTo("active"));
		Assert.That(await Read(plugin, "mutedeck-control-status"), Is.EqualTo("teams"));
		Assert.That(await Read(plugin, "mutedeck-call-label"), Is.EqualTo("In call"));
		Assert.That(await Read(plugin, "mutedeck-mute-label"), Is.EqualTo("Muted"));
		Assert.That(await Read(plugin, "mutedeck-video-label"), Is.EqualTo("Off"));
		Assert.That(await Read(plugin, "mutedeck-share-label"), Is.EqualTo("Disabled"));
		Assert.That(await Read(plugin, "mutedeck-record-label"), Is.EqualTo(""));
	}

	[Test]
	public async Task A_disabled_microphone_reads_as_no_mic_like_macro_deck_2()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect(MuteDeckStatus.Empty with { Mute = "disabled" });

		Assert.That(await Read(plugin, "mutedeck-mute-label"), Is.EqualTo("No mic"));
	}

	[Test]
	public async Task A_lost_connection_refreshes_every_variable()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		plugin.Connection.SetState(ConnectionState.Disconnected);

		Assert.That(plugin.Harness.Context.Variables.DefinitionRefreshRequests[^1], Has.Count.EqualTo(17));
	}

	private static async Task<object?> Read(TestPlugin plugin, string id) => (await plugin.Integration.ReadAsync(id)).Value;
}
