using MuteDeck.PluginClient;

namespace MuteDeckPlugin.Connection;

/// <summary>
/// The plugin's one connection to the MuteDeck desktop app. Everything the actions, variables and
/// issues report is read from here, so none of them ever touches the network while answering.
/// </summary>
internal interface IMuteDeckConnection
{
	ConnectionState ConnectionState { get; }

	/// <summary>True when actions can be sent: approved by MuteDeck, or an older MuteDeck without approval.</summary>
	bool IsReady { get; }

	MuteDeckStatus Status { get; }

	IReadOnlyList<string> CustomActions { get; }

	/// <summary>Raised on a background thread whenever anything above changes.</summary>
	event EventHandler<MuteDeckChangedEventArgs>? Changed;

	/// <summary>Connects, or keeps the running connection when it already targets <paramref name="endpoint"/>.</summary>
	Task StartAsync(MuteDeckEndpoint endpoint, ClientInfo client, ITokenStore tokenStore, CancellationToken cancellationToken);

	Task StopAsync();

	/// <param name="action">toggle_mute, toggle_video, toggle_share or toggle_record.</param>
	/// <param name="state">toggle, on or off.</param>
	Task<MuteDeckSendResult> ToggleAsync(string action, string state, CancellationToken cancellationToken);

	Task<MuteDeckSendResult> LeaveMeetingAsync(CancellationToken cancellationToken);

	Task<MuteDeckSendResult> BringToFrontAsync(CancellationToken cancellationToken);

	Task<MuteDeckSendResult> CustomActionAsync(string name, CancellationToken cancellationToken);

	Task RefreshCustomActionsAsync(CancellationToken cancellationToken);
}

internal sealed class MuteDeckChangedEventArgs : EventArgs
{
	public bool ConnectionChanged { get; init; }

	public MuteDeckStatusFields StatusFields { get; init; }

	public bool CustomActionsChanged { get; init; }
}
