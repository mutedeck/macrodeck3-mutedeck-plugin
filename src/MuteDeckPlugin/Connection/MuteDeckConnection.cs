using System.Net.WebSockets;
using MacroDeck.Sdk.Logging;
using Serilog;
using Sdk = MuteDeck.PluginClient;

namespace MuteDeckPlugin.Connection;

/// <summary>
/// Wraps <see cref="Sdk.MuteDeckClient"/>: protocol v2 with in-app approval, falling back to v1 on older
/// MuteDeck versions, reconnecting on its own. Status is kept as an immutable snapshot so readers on
/// other threads always see one consistent set of fields.
/// </summary>
internal sealed class MuteDeckConnection : IMuteDeckConnection, IAsyncDisposable
{
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _lifecycle = new(1, 1);
	private volatile FailureEpisodeTracker _unreachable = new();

	private Sdk.MuteDeckClient? _client;
	private MuteDeckEndpoint? _endpoint;
	private volatile string _endpointText = "";
	private volatile MuteDeckStatus _status = MuteDeckStatus.Empty;
	private volatile IReadOnlyList<string> _customActions = [];
	private volatile int _state = (int)Sdk.ConnectionState.Disconnected;

	public MuteDeckConnection(ILogger logger)
	{
		_logger = logger.ForContext<MuteDeckConnection>();
	}

	public Sdk.ConnectionState ConnectionState => (Sdk.ConnectionState)_state;

	public bool IsReady => IsReadyState(ConnectionState);

	public MuteDeckStatus Status => _status;

	public IReadOnlyList<string> CustomActions => _customActions;

	public event EventHandler<MuteDeckChangedEventArgs>? Changed;

	public async Task StartAsync(
		MuteDeckEndpoint endpoint,
		Sdk.ClientInfo client,
		Sdk.ITokenStore tokenStore,
		CancellationToken cancellationToken)
	{
		await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// InitializeAsync runs again on every reconnect and configuration change; reconnecting to the
			// same MuteDeck each time would drop a pending approval prompt.
			if (_client is not null && _endpoint == endpoint)
			{
				return;
			}

			await StopClientAsync().ConfigureAwait(false);

			// An outage of the previous endpoint says nothing about this one.
			_unreachable = new FailureEpisodeTracker();

			_logger.Information("Connecting to MuteDeck at {Endpoint} as {ClientId} {ClientVersion} ({HostApp} {HostVersion}, {Os})",
				endpoint, client.Id, client.Version, client.HostApp, client.HostVersion, client.Os);

			var created = new Sdk.MuteDeckClient(new Sdk.MuteDeckClientOptions
			{
				Client = client,
				Host = endpoint.Host,
				Port = endpoint.Port,
				TokenStore = tokenStore,
				Log = OnClientLog
			});
			created.StateChanged += OnStateChanged;
			created.StatusReceived += OnStatusReceived;
			created.CustomActionsReceived += OnCustomActionsReceived;
			created.Approved += OnApproved;
			created.Denied += OnDenied;
			created.Revoked += OnRevoked;
			_client = created;
			_endpoint = endpoint;
			_endpointText = endpoint.ToString();
			created.Start();
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public async Task StopAsync()
	{
		await _lifecycle.WaitAsync().ConfigureAwait(false);
		try
		{
			await StopClientAsync().ConfigureAwait(false);
		}
		finally
		{
			_lifecycle.Release();
		}
	}

	public Task<MuteDeckSendResult> ToggleAsync(string action, string state, CancellationToken cancellationToken)
	{
		return SendAsync(action,
			(client, token) => client.SendAsync(action, new Dictionary<string, object?> { ["state"] = state }, token),
			cancellationToken);
	}

	public Task<MuteDeckSendResult> LeaveMeetingAsync(CancellationToken cancellationToken)
	{
		return SendAsync("leave_meeting", (client, token) => client.LeaveMeetingAsync(token), cancellationToken);
	}

	public Task<MuteDeckSendResult> BringToFrontAsync(CancellationToken cancellationToken)
	{
		return SendAsync("bring-to-front", (client, token) => client.BringToFrontAsync(token), cancellationToken);
	}

	public Task<MuteDeckSendResult> CustomActionAsync(string name, CancellationToken cancellationToken)
	{
		return SendAsync("custom action " + name, (client, token) => client.CustomActionAsync(name, token), cancellationToken);
	}

	public async Task RefreshCustomActionsAsync(CancellationToken cancellationToken)
	{
		var client = _client;
		if (client is not { IsReady: true })
		{
			return;
		}

		try
		{
			ApplyCustomActions(client, await client.ListCustomActionsAsync(cancellationToken).ConfigureAwait(false));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception) when (exception is Sdk.MuteDeckRequestFailedException or Sdk.MuteDeckNotConnectedException
			or TimeoutException or WebSocketException or InvalidOperationException)
		{
			_logger.Debug("Could not read the custom actions from MuteDeck: {Message}", exception.Message);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync().ConfigureAwait(false);
		_lifecycle.Dispose();
	}

	private async Task<MuteDeckSendResult> SendAsync(
		string description,
		Func<Sdk.MuteDeckClient, CancellationToken, Task<Sdk.WsResponse>> send,
		CancellationToken cancellationToken)
	{
		var client = _client;
		if (client is not { IsReady: true })
		{
			return MuteDeckSendResult.NotConnected;
		}

		try
		{
			var response = await send(client, cancellationToken).ConfigureAwait(false);
			if (response.IsSuccess)
			{
				// A v1 MuteDeck sends no correlated answers, and the client reports that as 202.
				return response.Status == 202 ? MuteDeckSendResult.SentUnconfirmed : MuteDeckSendResult.Succeeded;
			}

			_logger.Information("MuteDeck answered {Action} with {Status} {Reason}", description, response.Status, response.Reason);
			return MuteDeckSendResult.Rejected(response.Reason);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Sdk.MuteDeckNotConnectedException)
		{
			return MuteDeckSendResult.NotConnected;
		}
		catch (TimeoutException)
		{
			_logger.Warning("MuteDeck did not answer {Action} in time", description);
			return MuteDeckSendResult.Timeout;
		}
		catch (Exception exception) when (exception is WebSocketException or InvalidOperationException or IOException)
		{
			_logger.Warning("Sending {Action} to MuteDeck failed: {Message}", description, exception.Message);
			return MuteDeckSendResult.Failed;
		}
	}

	private async Task StopClientAsync()
	{
		var client = _client;
		_client = null;
		_endpoint = null;
		if (client is not null)
		{
			client.StateChanged -= OnStateChanged;
			client.StatusReceived -= OnStatusReceived;
			client.CustomActionsReceived -= OnCustomActionsReceived;
			client.Approved -= OnApproved;
			client.Denied -= OnDenied;
			client.Revoked -= OnRevoked;
			try
			{
				await client.StopAsync().ConfigureAwait(false);
			}
			finally
			{
				client.Dispose();
			}
		}

		var stateChanged = Interlocked.Exchange(ref _state, (int)Sdk.ConnectionState.Disconnected)
			!= (int)Sdk.ConnectionState.Disconnected;
		if (stateChanged)
		{
			ResetStatusAndRaise(connectionChanged: true);
		}
	}

	private void OnStateChanged(object? sender, Sdk.ConnectionStateChangedEventArgs e)
	{
		if (sender is not Sdk.MuteDeckClient client || !ReferenceEquals(client, _client))
		{
			return;
		}

		var previous = (Sdk.ConnectionState)Interlocked.Exchange(ref _state, (int)e.State);
		if (previous == e.State)
		{
			return;
		}

		if (IsReadyState(e.State))
		{
			if (_unreachable.RecordSuccess() is { } end)
			{
				_logger.Information("Reached MuteDeck again after {Duration} ({Failures} failed attempts)", end.Duration, end.Failures);
			}

			_logger.Information(e.State == Sdk.ConnectionState.V1
				? "Connected to MuteDeck (older MuteDeck without approval)"
				: "Connected to MuteDeck");
		}
		else if (IsReadyState(previous))
		{
			_logger.Information("Disconnected from MuteDeck");
		}
		else if (e.State == Sdk.ConnectionState.Pending)
		{
			_logger.Information("Waiting for Macro Deck to be approved in MuteDeck");
		}

		if (IsReadyState(e.State) && !IsReadyState(previous))
		{
			Raise(new MuteDeckChangedEventArgs { ConnectionChanged = true });

			// v2 pushes the custom actions right after the welcome; v1 has to be asked.
			if (e.State == Sdk.ConnectionState.V1)
			{
				_ = RefreshCustomActionsAsync(CancellationToken.None);
			}

			return;
		}

		if (IsReadyState(previous) && !IsReadyState(e.State))
		{
			ResetStatusAndRaise(connectionChanged: true);
			return;
		}

		// While MuteDeck is closed the client cycles between connecting and disconnected on every retry;
		// nothing a consumer shows depends on that difference, so only approval changes are reported.
		if (IsReadyState(e.State) || IsApprovalState(previous) || IsApprovalState(e.State))
		{
			Raise(new MuteDeckChangedEventArgs { ConnectionChanged = true });
		}
	}

	private void OnStatusReceived(object? sender, Sdk.WsStatusUpdate update)
	{
		if (!ReferenceEquals(sender, _client))
		{
			return;
		}

		// MuteDeck may send partial updates, so only the fields present replace the stored ones.
		var previous = _status;
		var next = new MuteDeckStatus(
			update.Call ?? previous.Call,
			update.Mute ?? previous.Mute,
			update.Video ?? previous.Video,
			update.Share ?? previous.Share,
			update.Record ?? previous.Record,
			update.Control ?? previous.Control);
		_status = next;

		var changed = next.DifferencesFrom(previous);
		if (changed != MuteDeckStatusFields.None)
		{
			Raise(new MuteDeckChangedEventArgs { StatusFields = changed });
		}
	}

	private void OnCustomActionsReceived(object? sender, IReadOnlyList<Sdk.CustomAction> actions)
	{
		if (sender is Sdk.MuteDeckClient client && ReferenceEquals(client, _client))
		{
			ApplyCustomActions(client, actions);
		}
	}

	private void ApplyCustomActions(Sdk.MuteDeckClient client, IReadOnlyList<Sdk.CustomAction> actions)
	{
		if (!ReferenceEquals(client, _client))
		{
			return;
		}

		IReadOnlyList<string> next =
		[
			.. actions
				.Select(action => action.Name)
				.OfType<string>()
				.Where(name => !string.IsNullOrWhiteSpace(name))
				.Distinct(StringComparer.Ordinal)
		];
		if (next.SequenceEqual(_customActions, StringComparer.Ordinal))
		{
			return;
		}

		_customActions = next;
		Raise(new MuteDeckChangedEventArgs { CustomActionsChanged = true });
	}

	private void OnApproved(object? sender, EventArgs e)
	{
		_logger.Information("Macro Deck was approved in MuteDeck");
	}

	private void OnDenied(object? sender, Sdk.DeniedEventArgs e)
	{
		_logger.Warning("MuteDeck denied access ({Reason}); it asks again later", e.Reason);
	}

	private void OnRevoked(object? sender, EventArgs e)
	{
		_logger.Warning("Access was revoked in MuteDeck; asking for approval again");
	}

	private void OnClientLog(Sdk.MuteDeckLogLevel level, string message)
	{
		switch (level)
		{
			case Sdk.MuteDeckLogLevel.Debug:
				_logger.Debug("MuteDeck client: {Message}", message);
				break;
			case Sdk.MuteDeckLogLevel.Info:
				_logger.Information("MuteDeck client: {Message}", message);
				break;
			default:
				LogFailure(level, message);
				break;
		}
	}

	// The client retries on its own while MuteDeck is closed, so one line per retry would flood the
	// rate-limited log; an episode logs its start, a periodic summary and its end.
	private void LogFailure(Sdk.MuteDeckLogLevel level, string message)
	{
		if (IsReady)
		{
			_logger.Warning("MuteDeck client: {Message}", message);
			return;
		}

		var signal = _unreachable.RecordFailure(message);
		switch (signal.Kind)
		{
			case FailureEpisodeSignalKind.Onset:
				_logger.Warning("Cannot reach MuteDeck at {Endpoint}: {Message}. Retrying in the background.", _endpointText, message);
				break;
			case FailureEpisodeSignalKind.SummaryDue:
				_logger.Warning("Still cannot reach MuteDeck at {Endpoint} after {Duration} ({Failures} attempts): {Message}",
					_endpointText, signal.Duration, signal.ConsecutiveFailures, message);
				break;
			default:
				_logger.Debug("MuteDeck client ({Level}): {Message}", level, message);
				break;
		}
	}

	private void ResetStatusAndRaise(bool connectionChanged)
	{
		var previous = _status;
		_status = MuteDeckStatus.Empty;
		var hadActions = _customActions.Count > 0;
		_customActions = [];
		Raise(new MuteDeckChangedEventArgs
		{
			ConnectionChanged = connectionChanged,
			StatusFields = MuteDeckStatus.Empty.DifferencesFrom(previous),
			CustomActionsChanged = hadActions
		});
	}

	private void Raise(MuteDeckChangedEventArgs args)
	{
		try
		{
			Changed?.Invoke(this, args);
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{
			// A subscriber failing must not take the client's receive loop down with it.
			_logger.Warning(exception, "A MuteDeck change handler failed");
		}
	}

	private static bool IsReadyState(Sdk.ConnectionState state)
	{
		return state is Sdk.ConnectionState.Authenticated or Sdk.ConnectionState.V1;
	}

	private static bool IsApprovalState(Sdk.ConnectionState state)
	{
		return state is Sdk.ConnectionState.Pending or Sdk.ConnectionState.Denied;
	}
}
