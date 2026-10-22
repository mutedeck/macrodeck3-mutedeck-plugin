using MacroDeck.Sdk.ConfigFlow;
using MuteDeck.PluginClient;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class ConfigFlowTests
{
	[Test]
	public async Task Setup_is_required_and_single()
	{
		await using var plugin = TestPlugin.Create();
		IConfigFlowProvider provider = plugin.Integration;

		Assert.That(provider.RequiresConfiguration, Is.True);
		Assert.That(provider.AllowsMultipleConfigurations, Is.False);
	}

	[Test]
	public async Task The_step_is_prefilled_with_the_defaults()
	{
		await using var plugin = TestPlugin.Create();

		var result = await plugin.Integration.CreateConfigFlow().StartAsync(new FlowContext(), CancellationToken.None);

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Step));
		var fields = result.NextStep!.Fields.ToDictionary(field => field.Name);
		Assert.That(fields["host"].DefaultValue, Is.EqualTo("localhost"));
		Assert.That(fields["port"].DefaultValue, Is.EqualTo(3492d));
	}

	[Test]
	public async Task Saving_the_defaults_completes()
	{
		await using var plugin = TestPlugin.Create();

		var result = await Submit(plugin, "localhost", 3492d);

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Complete));
		Assert.That(result.EntryTitle, Is.EqualTo("MuteDeck"));
		Assert.That(result.Values!["host"].Value, Is.EqualTo("localhost"));
		Assert.That(result.Values["port"].Value, Is.EqualTo("3492"));
		Assert.That(result.Values.ContainsKey("mutedeck_token"), Is.False);
	}

	[TestCase("", 3492d, "host")]
	[TestCase("http://localhost", 3492d, "host")]
	[TestCase("localhost", 0d, "port")]
	[TestCase("localhost", 70000d, "port")]
	[TestCase("localhost", 34.5d, "port")]
	public async Task Invalid_values_are_field_errors(string host, double port, string field)
	{
		await using var plugin = TestPlugin.Create();

		var result = await Submit(plugin, host, port);

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
		Assert.That(result.FieldErrors!.Keys, Is.EqualTo(new[] { field }));
	}

	[Test]
	public async Task Saving_the_same_endpoint_keeps_the_approval()
	{
		await using var plugin = TestPlugin.Create();
		plugin.AddEntry(token: "token-1", instanceId: "instance-1");
		await plugin.Harness.InitializeIntegrationsAsync();
		await plugin.TokenStore.LoadAsync();

		var result = await Submit(plugin, "localhost", 3492d);

		Assert.That(result.Values!["mutedeck_token"], Is.EqualTo(ConfigFlowValue.Secret("token-1")));
		Assert.That(result.Values["mutedeck_instance_id"], Is.EqualTo(ConfigFlowValue.Secret("instance-1")));
	}

	[Test]
	public async Task Another_endpoint_drops_the_token_but_keeps_the_instance_id()
	{
		await using var plugin = TestPlugin.Create();
		plugin.AddEntry(token: "token-1", instanceId: "instance-1");
		await plugin.Harness.InitializeIntegrationsAsync();
		await plugin.TokenStore.LoadAsync();

		var result = await Submit(plugin, "studio-pc", 3492d);

		Assert.That(result.Values!["mutedeck_token"], Is.EqualTo(ConfigFlowValue.Secret(string.Empty)));
		Assert.That(result.Values["mutedeck_instance_id"], Is.EqualTo(ConfigFlowValue.Secret("instance-1")));
	}

	[Test]
	public async Task After_moving_to_another_endpoint_the_old_token_is_never_loaded_again()
	{
		await using var plugin = TestPlugin.Create();
		var entryId = plugin.AddEntry(token: "token-1", instanceId: "instance-1");
		await plugin.Harness.InitializeIntegrationsAsync();
		await plugin.TokenStore.LoadAsync();

		var result = await Submit(plugin, "studio-pc", 3492d);
		var config = plugin.Harness.Context.Config;
		foreach (var (key, value) in result.Values!)
		{
			if (value.IsSecret)
			{
				config.SeedSecret(entryId, key, value.Value ?? string.Empty);
			}
			else
			{
				config.SeedString(entryId, key, value.Value ?? string.Empty);
			}
		}

		await plugin.Harness.InitializeIntegrationsAsync();
		var loaded = await plugin.TokenStore.LoadAsync();

		Assert.That(plugin.Connection.Starts[^1].Endpoint.Host, Is.EqualTo("studio-pc"));
		Assert.That(loaded.Token, Is.Null);
		Assert.That(loaded.InstanceId, Is.EqualTo("instance-1"));
	}

	[Test]
	public async Task An_unknown_step_is_an_error()
	{
		await using var plugin = TestPlugin.Create();

		var result = await plugin.Integration.CreateConfigFlow()
			.SubmitAsync("elsewhere", new Dictionary<string, object?>(), new FlowContext(), CancellationToken.None);

		Assert.That(result.Kind, Is.EqualTo(ConfigFlowResultKind.Error));
	}

	private static Task<ConfigFlowResult> Submit(TestPlugin plugin, string host, double port)
	{
		return plugin.Integration.CreateConfigFlow().SubmitAsync("connection",
			new Dictionary<string, object?> { ["host"] = host, ["port"] = port },
			new FlowContext(),
			CancellationToken.None);
	}

	private sealed class FlowContext : IConfigFlowContext
	{
		public IOAuthSession OAuth => throw new NotSupportedException("The MuteDeck setup has no OAuth step.");
	}
}
