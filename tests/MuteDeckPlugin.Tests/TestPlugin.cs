using System.Text.Json;
using MacroDeck.Plugin.Testing;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Microsoft.Extensions.DependencyInjection;
using MuteDeckPlugin.Connection;
using MuteDeckPlugin.Setup;

namespace MuteDeckPlugin.Tests;

/// <summary>The plugin as Program.cs builds it, with MuteDeck replaced by <see cref="FakeMuteDeckConnection"/>.</summary>
internal sealed class TestPlugin : IAsyncDisposable
{
	public const string PluginId = "com.mutedeck.mutedeck";
	public const string PluginVersion = "1.2.3";

	private readonly PluginTestManifest _manifest;

	private TestPlugin(PluginTestManifest manifest, PluginTestHarness harness, FakeMuteDeckConnection connection)
	{
		_manifest = manifest;
		Harness = harness;
		Connection = connection;
	}

	public PluginTestHarness Harness { get; }

	public FakeMuteDeckConnection Connection { get; }

	public MuteDeckIntegration Integration =>
		Harness.Services.GetServices<IPluginIntegration>().OfType<MuteDeckIntegration>().Single();

	public HostTokenStore TokenStore => Harness.Services.GetRequiredService<HostTokenStore>();

	public static TestPlugin Create()
	{
		var connection = new FakeMuteDeckConnection();
		var manifest = new PluginTestManifest(id: PluginId, name: "MuteDeck", version: PluginVersion);
		var harness = PluginTestHarness.Create(builder =>
		{
			builder.UseLocalization(Strings.LocalizationCatalog).RegisterIntegration<MuteDeckIntegration>();
			builder.Services.AddMuteDeck();
			builder.Services.AddSingleton<IMuteDeckConnection>(connection);
		}, manifest);
		return new TestPlugin(manifest, harness, connection);
	}

	/// <summary>Creates the config entry the setup flow would, optionally with stored values.</summary>
	public Guid AddEntry(string? host = null, string? port = null, string? token = null, string? instanceId = null)
	{
		var config = Harness.Context.Config;
		var entryId = config.AddEntry(MuteDeckSettings.EntryTitle);
		if (host is not null)
		{
			config.SeedString(entryId, MuteDeckSettings.HostKey, host);
		}

		if (port is not null)
		{
			config.SeedString(entryId, MuteDeckSettings.PortKey, port);
		}

		if (token is not null)
		{
			config.SeedSecret(entryId, MuteDeckSettings.TokenKey, token);
		}

		if (instanceId is not null)
		{
			config.SeedSecret(entryId, MuteDeckSettings.InstanceIdKey, instanceId);
		}

		return entryId;
	}

	public async Task<TestPlugin> InitializeConfiguredAsync()
	{
		AddEntry();
		await Harness.InitializeIntegrationsAsync();
		return this;
	}

	public T Action<T>(string id) where T : class
		=> Integration.Actions.Single(action => action.Id == id) as T
			?? throw new InvalidOperationException($"{id} is not a {typeof(T).Name}");

	public async Task<ActionStateSnapshot> StateOf(string actionId, Dictionary<string, object?>? parameters = null)
		=> (await Action<IStateProviderActionDefinition>(actionId)
			.GetActionStateAsync(parameters ?? [], CancellationToken.None))!;

	public Task<ActionIconSnapshot?> IconOf(string actionId, Dictionary<string, object?>? parameters = null)
		=> Action<IIconProviderActionDefinition>(actionId).GetActionIconAsync(parameters ?? [], CancellationToken.None);

	public async ValueTask DisposeAsync()
	{
		await Harness.DisposeAsync();
		_manifest.Dispose();
	}

	/// <summary>Reads a property of a wire payload without depending on the protocol's DTO types.</summary>
	public static JsonElement? Property(JsonElement? element, string name)
	{
		if (element is not { ValueKind: JsonValueKind.Object } value)
		{
			return null;
		}

		foreach (var property in value.EnumerateObject())
		{
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return property.Value;
			}
		}

		return null;
	}
}
