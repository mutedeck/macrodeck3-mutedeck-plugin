using MuteDeckPlugin.Connection;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class LifecycleTests
{
	[Test]
	public async Task Without_a_config_entry_nothing_connects()
	{
		await using var plugin = TestPlugin.Create();

		await plugin.Harness.InitializeIntegrationsAsync();

		Assert.That(plugin.Connection.Starts, Is.Empty);
		Assert.That(plugin.Connection.Stops, Is.EqualTo(1));
	}

	[Test]
	public async Task An_entry_without_values_connects_to_the_default_endpoint()
	{
		await using var plugin = TestPlugin.Create();

		await plugin.InitializeConfiguredAsync();

		Assert.That(plugin.Connection.Starts, Has.Count.EqualTo(1));
		Assert.That(plugin.Connection.Starts[0].Endpoint, Is.EqualTo(new MuteDeckEndpoint("localhost", 3492)));
		Assert.That(plugin.Connection.Starts[0].TokenStore, Is.SameAs(plugin.TokenStore));
	}

	[Test]
	public async Task Stored_host_and_port_are_used()
	{
		await using var plugin = TestPlugin.Create();
		plugin.AddEntry(host: "studio-pc", port: "4000");

		await plugin.Harness.InitializeIntegrationsAsync();

		Assert.That(plugin.Connection.Starts[0].Endpoint, Is.EqualTo(new MuteDeckEndpoint("studio-pc", 4000)));
	}

	[Test]
	public async Task A_port_stored_as_a_json_number_is_read()
	{
		await using var plugin = TestPlugin.Create();
		plugin.AddEntry(port: "4000.0");

		await plugin.Harness.InitializeIntegrationsAsync();

		Assert.That(plugin.Connection.Starts[0].Endpoint.Port, Is.EqualTo(4000));
	}

	[Test]
	public async Task Initializing_again_is_safe_and_hands_the_same_endpoint_to_the_connection()
	{
		await using var plugin = TestPlugin.Create();
		await plugin.InitializeConfiguredAsync();

		await plugin.Harness.InitializeIntegrationsAsync();

		Assert.That(plugin.Connection.Starts, Has.Count.EqualTo(2));
		Assert.That(plugin.Connection.Starts[1].Endpoint, Is.EqualTo(plugin.Connection.Starts[0].Endpoint));
	}

	[Test]
	public async Task The_client_identifies_as_macro_deck_with_the_plugin_version()
	{
		await using var plugin = TestPlugin.Create();
		plugin.Harness.Context.Http.UserAgent = "MacroDeck/3.0.0-beta.18";

		await plugin.InitializeConfiguredAsync();

		var client = plugin.Connection.Starts[0].Client;
		Assert.That(client.Id, Is.EqualTo("macro-deck"));
		Assert.That(client.Name, Is.EqualTo("Macro Deck"));
		Assert.That(client.HostApp, Is.EqualTo("macro-deck"));
		Assert.That(client.Version, Is.EqualTo(TestPlugin.PluginVersion));
		Assert.That(client.HostVersion, Is.EqualTo("3.0.0-beta.18"));
	}

	[Test]
	public async Task Shutdown_stops_the_connection()
	{
		await using var plugin = TestPlugin.Create();
		await plugin.InitializeConfiguredAsync();

		await plugin.Integration.ShutdownAsync();

		Assert.That(plugin.Connection.Stops, Is.EqualTo(1));
	}

	[Test]
	public async Task A_connection_change_asks_the_host_to_read_the_variables_again()
	{
		await using var plugin = TestPlugin.Create();
		await plugin.InitializeConfiguredAsync();
		var before = plugin.Harness.Context.Variables.DefinitionRefreshRequests.Count;

		plugin.Connection.SetStatus(MuteDeckStatus.Empty with { Mute = "active" });

		var requests = plugin.Harness.Context.Variables.DefinitionRefreshRequests;
		Assert.That(requests, Has.Count.EqualTo(before + 1));
		Assert.That(requests[^1], Is.EquivalentTo(new[] { "mutedeck-muted", "mutedeck-mute-status", "mutedeck-mute-label" }));
	}

	[Test]
	public async Task A_status_change_invalidates_only_the_icons_that_follow_it()
	{
		await using var plugin = TestPlugin.Create();

		var mute = plugin.Integration.IconsAffectedBy(new MuteDeckChangedEventArgs { StatusFields = MuteDeckStatusFields.Mute });
		var call = plugin.Integration.IconsAffectedBy(new MuteDeckChangedEventArgs { StatusFields = MuteDeckStatusFields.Call });
		var connection = plugin.Integration.IconsAffectedBy(new MuteDeckChangedEventArgs { ConnectionChanged = true });
		var actions = plugin.Integration.IconsAffectedBy(new MuteDeckChangedEventArgs { CustomActionsChanged = true });

		Assert.That(mute, Is.EqualTo(new[] { "toggle-mute" }));
		Assert.That(call, Is.EquivalentTo(new[] { "leave-meeting", "bring-to-front" }));
		Assert.That(connection, Has.Exactly(7).Items);
		Assert.That(actions, Is.Empty);
	}
}
