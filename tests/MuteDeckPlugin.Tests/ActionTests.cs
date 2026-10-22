using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Actions;
using MuteDeckPlugin.Connection;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class ActionTests
{
	[Test]
	public async Task The_plugin_declares_the_seven_macro_deck_2_actions()
	{
		await using var plugin = TestPlugin.Create();

		Assert.That(plugin.Integration.Actions.Select(action => action.Id), Is.EqualTo(new[]
		{
			"toggle-mute", "toggle-video", "toggle-share", "toggle-record", "leave-meeting", "bring-to-front", "custom-action"
		}));
	}

	[TestCase("toggle-mute", "toggle_mute")]
	[TestCase("toggle-video", "toggle_video")]
	[TestCase("toggle-share", "toggle_share")]
	[TestCase("toggle-record", "toggle_record")]
	public async Task A_toggle_sends_its_muteDeck_action_with_the_chosen_state(string actionId, string wireAction)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		var outcome = await plugin.Harness.Actions.ExecuteAsync(actionId, new Dictionary<string, object?> { ["state"] = "on" });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(plugin.Connection.Sent, Is.EqualTo(new[] { $"{wireAction}:on" }));
	}

	[Test]
	public async Task A_toggle_without_a_state_toggles()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		await plugin.Harness.Actions.ExecuteAsync("toggle-mute");

		Assert.That(plugin.Connection.Sent, Is.EqualTo(new[] { "toggle_mute:toggle" }));
	}

	[Test]
	public async Task Toggling_reports_the_state_the_button_should_end_in()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect(MuteDeckStatus.Empty with { Mute = "inactive" });

		var outcome = await plugin.Harness.Actions.ExecuteAsync("toggle-mute", new Dictionary<string, object?> { ["state"] = "toggle" });

		Assert.That(TestPlugin.Property(outcome.Data, "expectedStateId")?.GetString(), Is.EqualTo("muted"));
	}

	[Test]
	public async Task An_unknown_state_is_an_invalid_parameter_and_sends_nothing()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("toggle-video", new Dictionary<string, object?> { ["state"] = "sideways" });

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Code, Is.EqualTo(ActionErrorCodes.InvalidParameter));
		Assert.That(plugin.Connection.Sent, Is.Empty);
	}

	[TestCase("toggle-mute")]
	[TestCase("leave-meeting")]
	[TestCase("bring-to-front")]
	public async Task Nothing_is_claimed_while_muteDeck_is_not_connected(string actionId)
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync(actionId);

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Code, Is.EqualTo(ActionErrorCodes.NotConnected));
		Assert.That(plugin.Connection.Sent, Is.Empty);
	}

	[Test]
	public async Task A_rejection_is_reported_as_a_failure()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();
		plugin.Connection.NextResult = MuteDeckSendResult.Rejected("not_in_call");

		var outcome = await plugin.Harness.Actions.ExecuteAsync("leave-meeting");

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error!.Code, Is.EqualTo(ActionErrorCodes.ProviderRejected));
	}

	[Test]
	public async Task A_timeout_is_reported_as_a_timeout()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();
		plugin.Connection.NextResult = MuteDeckSendResult.Timeout;

		var outcome = await plugin.Harness.Actions.ExecuteAsync("bring-to-front");

		Assert.That(outcome.Error!.Code, Is.EqualTo(ActionErrorCodes.Timeout));
	}

	[Test]
	public async Task An_older_muteDeck_that_cannot_confirm_gets_accepted_rather_than_success()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();
		plugin.Connection.NextResult = MuteDeckSendResult.SentUnconfirmed;

		var outcome = await plugin.Harness.Actions.ExecuteAsync("bring-to-front");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(TestPlugin.Property(outcome.Data, "accepted")?.GetBoolean(), Is.True);
	}

	[Test]
	public async Task A_custom_action_runs_by_name()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("custom-action", new Dictionary<string, object?> { ["name"] = " raise-hand " });

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(plugin.Connection.Sent, Is.EqualTo(new[] { "custom:raise-hand" }));
	}

	[Test]
	public async Task A_custom_action_without_a_name_fails()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("custom-action", new Dictionary<string, object?> { ["name"] = "" });

		Assert.That(outcome.Error!.Code, Is.EqualTo(ActionErrorCodes.InvalidParameter));
		Assert.That(plugin.Connection.Sent, Is.Empty);
	}

	[Test]
	public async Task Custom_action_options_come_from_muteDeck_and_allow_typing_a_name()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();
		plugin.Connection.CustomActions = ["raise-hand", "toggle-chat", "react-heart"];
		var action = plugin.Action<CustomActionAction>("custom-action");

		var result = await action.GetDynamicOptionsAsync(
			new DynamicOptionsContext { ParameterName = "name", Filter = "TOGGLE", CurrentParameters = new Dictionary<string, object?>() },
			CancellationToken.None);

		Assert.That(result.Options.Select(option => option.Value), Is.EqualTo(new[] { "toggle-chat" }));
		Assert.That(result.AllowsCustomValue, Is.True);
	}

	[Test]
	public async Task Custom_action_options_explain_a_missing_connection()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		var action = plugin.Action<CustomActionAction>("custom-action");

		var result = await action.GetDynamicOptionsAsync(
			new DynamicOptionsContext { ParameterName = "name", CurrentParameters = new Dictionary<string, object?>() },
			CancellationToken.None);

		Assert.That(result.Options, Is.Empty);
		Assert.That(result.Error.IsEmpty, Is.False);
		Assert.That(result.AllowsCustomValue, Is.True);
	}

	[Test]
	public async Task An_empty_custom_action_list_is_requested_from_muteDeck()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.Connect();
		var action = plugin.Action<CustomActionAction>("custom-action");

		await action.GetDynamicOptionsAsync(
			new DynamicOptionsContext { ParameterName = "name", CurrentParameters = new Dictionary<string, object?>() },
			CancellationToken.None);

		Assert.That(plugin.Connection.CustomActionRefreshes, Is.EqualTo(1));
	}
}
