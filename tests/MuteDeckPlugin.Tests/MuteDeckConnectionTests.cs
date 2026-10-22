using MuteDeck.PluginClient;
using MuteDeckPlugin.Connection;
using NUnit.Framework;
using Serilog;

namespace MuteDeckPlugin.Tests;

/// <summary>The real connection, pointed at a port nothing listens on, so no MuteDeck is needed.</summary>
[TestFixture]
public sealed class MuteDeckConnectionTests
{
	private static readonly MuteDeckEndpoint _nowhere = new("127.0.0.1", 1);

	[Test]
	public async Task Nothing_is_sent_before_muteDeck_is_reached()
	{
		using var logger = new LoggerConfiguration().CreateLogger();
		await using var connection = new MuteDeckConnection(logger);

		var result = await connection.ToggleAsync("toggle_mute", "toggle", CancellationToken.None);

		Assert.That(result.Outcome, Is.EqualTo(MuteDeckSendOutcome.NotConnected));
		Assert.That(connection.IsReady, Is.False);
		Assert.That(connection.Status, Is.EqualTo(MuteDeckStatus.Empty));
	}

	[Test]
	public async Task Starting_twice_with_the_same_endpoint_and_stopping_is_safe()
	{
		using var logger = new LoggerConfiguration().CreateLogger();
		await using var connection = new MuteDeckConnection(logger);
		var store = new InMemoryTokenStore();
		var client = ClientInfoFactory.Create("1.0.0", null);

		await connection.StartAsync(_nowhere, client, store, CancellationToken.None);
		await connection.StartAsync(_nowhere, client, store, CancellationToken.None);
		await connection.StopAsync();
		await connection.StopAsync();

		Assert.That(connection.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
		Assert.That(connection.CustomActions, Is.Empty);
	}
}
