using MacroDeck.Sdk.ConfigFlow;
using MuteDeck.PluginClient;
using MuteDeckPlugin.Connection;
using NUnit.Framework;
using Serilog;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class TokenStoreTests
{
	[Test]
	public async Task Stored_secrets_are_loaded()
	{
		await using var plugin = TestPlugin.Create();
		plugin.AddEntry(token: "token-1", instanceId: "instance-1");
		await plugin.Harness.InitializeIntegrationsAsync();

		var loaded = await plugin.TokenStore.LoadAsync();

		Assert.That(loaded.Token, Is.EqualTo("token-1"));
		Assert.That(loaded.InstanceId, Is.EqualTo("instance-1"));
	}

	[Test]
	public async Task A_new_approval_is_saved_as_secrets_of_the_entry()
	{
		await using var plugin = TestPlugin.Create();
		var entryId = plugin.AddEntry();
		await plugin.Harness.InitializeIntegrationsAsync();

		await plugin.TokenStore.SaveAsync(new StoredCredentials { Token = "token-2", InstanceId = "instance-2" });

		var config = plugin.Harness.Context.Config;
		Assert.That(await config.GetSecretAsync(entryId, "mutedeck_token"), Is.EqualTo("token-2"));
		Assert.That(await config.GetSecretAsync(entryId, "mutedeck_instance_id"), Is.EqualTo("instance-2"));
		Assert.That(await config.GetStringAsync(entryId, "mutedeck_token"), Is.Null);
	}

	[Test]
	public async Task Clearing_empties_the_stored_secrets()
	{
		await using var plugin = TestPlugin.Create();
		var entryId = plugin.AddEntry(token: "token-1", instanceId: "instance-1");
		await plugin.Harness.InitializeIntegrationsAsync();

		await plugin.TokenStore.ClearAsync();

		Assert.That(await plugin.Harness.Context.Config.GetSecretAsync(entryId, "mutedeck_token"), Is.Empty);
		var loaded = await plugin.TokenStore.LoadAsync();
		Assert.That(loaded.Token, Is.Null);
		Assert.That(loaded.InstanceId, Is.Null);
	}

	[Test]
	public async Task An_empty_stored_token_wins_over_the_copy_in_memory()
	{
		await using var plugin = TestPlugin.Create();
		var entryId = plugin.AddEntry();
		await plugin.Harness.InitializeIntegrationsAsync();
		await plugin.TokenStore.SaveAsync(new StoredCredentials { Token = "token-old", InstanceId = "instance-1" });

		plugin.Harness.Context.Config.SeedSecret(entryId, "mutedeck_token", string.Empty);

		Assert.That((await plugin.TokenStore.LoadAsync()).Token, Is.Null);
	}

	[Test]
	public async Task A_host_failure_keeps_the_credentials_in_memory()
	{
		using var logger = new LoggerConfiguration().CreateLogger();
		var store = new HostTokenStore(logger);
		store.Bind(new UnreachableConfig(), Guid.NewGuid());

		Assert.DoesNotThrowAsync(() => store.SaveAsync(new StoredCredentials { Token = "token-3", InstanceId = "instance-3" }));
		var loaded = await store.LoadAsync();

		Assert.That(loaded.Token, Is.EqualTo("token-3"));
		Assert.That(loaded.InstanceId, Is.EqualTo("instance-3"));
	}

	[Test]
	public async Task Without_an_entry_the_store_works_in_memory()
	{
		using var logger = new LoggerConfiguration().CreateLogger();
		var store = new HostTokenStore(logger);

		await store.SaveAsync(new StoredCredentials { Token = "token-4" });

		Assert.That((await store.LoadAsync()).Token, Is.EqualTo("token-4"));
	}

	/// <summary>Every call fails the way a host call does when the host is unreachable.</summary>
	private sealed class UnreachableConfig : IIntegrationConfig
	{
		public Task<IReadOnlyList<ConfigEntrySnapshot>> GetEntriesAsync(CancellationToken cancellationToken = default)
			=> throw Failure();

		public Task<string?> GetStringAsync(Guid entryId, string key, CancellationToken cancellationToken = default)
			=> throw Failure();

		public Task<string?> GetSecretAsync(Guid entryId, string key, CancellationToken cancellationToken = default)
			=> throw Failure();

		public Task SetStringAsync(Guid entryId, string key, string? value, CancellationToken cancellationToken = default)
			=> throw Failure();

		public Task SetSecretAsync(Guid entryId, string key, string value, CancellationToken cancellationToken = default)
			=> throw Failure();

		private static InvalidOperationException Failure() => new("The host is not connected.");
	}
}
