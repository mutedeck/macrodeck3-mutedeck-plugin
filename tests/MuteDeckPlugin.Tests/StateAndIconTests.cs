using MacroDeck.Sdk.Actions;
using MuteDeck.PluginClient;
using MuteDeckPlugin.Connection;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class StateAndIconTests
{
	[TestCase("active", "muted", "muted")]
	[TestCase("inactive", "unmuted", "unmuted")]
	[TestCase("disabled", "no-mic", "microphone-disabled")]
	public async Task The_mute_button_follows_the_microphone(string status, string state, string icon)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect(MuteDeckStatus.Empty with { Mute = status });

		Assert.That((await plugin.StateOf("toggle-mute")).ActiveStateId, Is.EqualTo(state));
		Assert.That(await plugin.IconOf("toggle-mute"), Is.EqualTo(Icon(icon)));
	}

	[TestCase("toggle-video", "video-started", "video-stopped", "video-disabled")]
	[TestCase("toggle-share", "sharing-started", "sharing-stopped", "sharing-disabled")]
	[TestCase("toggle-record", "recording-started", "recording-stopped", "recording-disabled")]
	public async Task The_on_off_toggles_follow_their_status(string actionId, string onIcon, string offIcon, string disabledIcon)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		foreach (var (status, state, icon) in new[] { ("active", "on", onIcon), ("inactive", "off", offIcon), ("disabled", "disabled", disabledIcon) })
		{
			plugin.Connection.Connect(new MuteDeckStatus("active", status, status, status, status, "zoom"));

			Assert.That((await plugin.StateOf(actionId)).ActiveStateId, Is.EqualTo(state));
			Assert.That(await plugin.IconOf(actionId), Is.EqualTo(Icon(icon)));
		}
	}

	[TestCase("toggle-mute")]
	[TestCase("toggle-video")]
	[TestCase("leave-meeting")]
	public async Task Without_a_connection_the_state_is_unavailable_and_the_button_keeps_its_icon(string actionId)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.SetState(ConnectionState.Pending);

		Assert.That((await plugin.StateOf(actionId)).ActiveStateId, Is.EqualTo("unavailable"));
		Assert.That(await plugin.IconOf(actionId), Is.Null);
	}

	[Test]
	public async Task Leave_meeting_shows_whether_there_is_a_call()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		plugin.Connection.Connect(MuteDeckStatus.Empty with { Call = "active" });
		Assert.That((await plugin.StateOf("leave-meeting")).ActiveStateId, Is.EqualTo("in-call"));
		Assert.That(await plugin.IconOf("leave-meeting"), Is.EqualTo(Icon("leave-meeting")));
		Assert.That(await plugin.IconOf("bring-to-front"), Is.EqualTo(Icon("bring-to-front")));

		plugin.Connection.SetStatus(MuteDeckStatus.Empty with { Call = "inactive" });
		Assert.That((await plugin.StateOf("leave-meeting")).ActiveStateId, Is.EqualTo("no-call"));
		Assert.That(await plugin.IconOf("leave-meeting"), Is.EqualTo(Icon("leave-meeting-disabled")));
		Assert.That(await plugin.IconOf("bring-to-front"), Is.EqualTo(Icon("bring-to-front-disabled")));
	}

	[TestCase("raise-hand", "raise-hand")]
	[TestCase("React-Heart", "react-heart")]
	[TestCase("my own shortcut", "custom-action")]
	public async Task A_custom_action_shows_its_own_icon_when_the_pack_has_one(string name, string icon)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		Assert.That(await plugin.IconOf("custom-action", new Dictionary<string, object?> { ["name"] = name }), Is.EqualTo(Icon(icon)));
	}

	[Test]
	public async Task Every_declared_state_is_offered_with_unavailable_last()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		var states = (await plugin.StateOf("toggle-mute")).States.Select(state => state.Id);

		Assert.That(states, Is.EqualTo(new[] { "muted", "unmuted", "no-mic", "unavailable" }));
	}

	private static ActionIconSnapshot Icon(string name) => new()
	{
		Version = name,
		Reference = ActionIconReference.PluginIcon("mutedeck", name)
	};
}
