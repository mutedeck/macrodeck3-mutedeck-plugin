using MuteDeck.PluginClient;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Tests;

/// <summary>Stands in for MuteDeck: tests set the state and the answer, and read back what was sent.</summary>
internal sealed class FakeMuteDeckConnection : IMuteDeckConnection
{
	private readonly List<string> _sent = [];

	public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

	public bool IsReady => ConnectionState is ConnectionState.Authenticated or ConnectionState.V1;

	public MuteDeckStatus Status { get; private set; } = MuteDeckStatus.Empty;

	public IReadOnlyList<string> CustomActions { get; set; } = [];

	public MuteDeckSendResult NextResult { get; set; } = MuteDeckSendResult.Succeeded;

	public IReadOnlyList<string> Sent => _sent;

	public List<(MuteDeckEndpoint Endpoint, ClientInfo Client, ITokenStore TokenStore)> Starts { get; } = [];

	public int Stops { get; private set; }

	public int CustomActionRefreshes { get; private set; }

	public event EventHandler<MuteDeckChangedEventArgs>? Changed;

	public void SetState(ConnectionState state)
	{
		ConnectionState = state;
		if (!IsReady)
		{
			Status = MuteDeckStatus.Empty;
		}

		Changed?.Invoke(this, new MuteDeckChangedEventArgs { ConnectionChanged = true, StatusFields = MuteDeckStatusFields.All });
	}

	public void SetStatus(MuteDeckStatus status)
	{
		var changed = status.DifferencesFrom(Status);
		Status = status;
		Changed?.Invoke(this, new MuteDeckChangedEventArgs { StatusFields = changed });
	}

	public void Connect(MuteDeckStatus? status = null)
	{
		SetState(ConnectionState.Authenticated);
		SetStatus(status ?? new MuteDeckStatus("active", "inactive", "inactive", "inactive", "inactive", "zoom"));
	}

	public Task StartAsync(MuteDeckEndpoint endpoint, ClientInfo client, ITokenStore tokenStore, CancellationToken cancellationToken)
	{
		Starts.Add((endpoint, client, tokenStore));
		return Task.CompletedTask;
	}

	public Task StopAsync()
	{
		Stops++;
		return Task.CompletedTask;
	}

	public Task<MuteDeckSendResult> ToggleAsync(string action, string state, CancellationToken cancellationToken)
		=> Send($"{action}:{state}");

	public Task<MuteDeckSendResult> LeaveMeetingAsync(CancellationToken cancellationToken) => Send("leave_meeting");

	public Task<MuteDeckSendResult> BringToFrontAsync(CancellationToken cancellationToken) => Send("bring-to-front");

	public Task<MuteDeckSendResult> CustomActionAsync(string name, CancellationToken cancellationToken) => Send($"custom:{name}");

	public Task RefreshCustomActionsAsync(CancellationToken cancellationToken)
	{
		CustomActionRefreshes++;
		return Task.CompletedTask;
	}

	private Task<MuteDeckSendResult> Send(string what)
	{
		_sent.Add(what);
		return Task.FromResult(NextResult);
	}
}
