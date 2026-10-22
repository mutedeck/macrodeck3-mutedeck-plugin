using MacroDeck.Plugin.Hosting;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Issues;
using MacroDeck.Sdk.Migration;
using MacroDeck.Sdk.Notifications;
using MacroDeck.Sdk.Variables;
using MuteDeck.PluginClient;
using MuteDeckPlugin.Actions;
using MuteDeckPlugin.Connection;
using MuteDeckPlugin.Migration;
using MuteDeckPlugin.Setup;
using MuteDeckPlugin.Variables;
using Serilog;

namespace MuteDeckPlugin;

/// <summary>
/// Controls meetings through the MuteDeck desktop app: actions for the meeting controls, live button
/// states and icons, the Macro Deck 2 variables, and the approval prompt MuteDeck shows on first use.
/// </summary>
internal sealed class MuteDeckIntegration : IPluginIntegration, IVariableProvider, IConfigFlowProvider,
	IIntegrationIssueProvider, IMigrationProvider
{
	internal const string ApprovalPendingIssueId = "approve-in-mutedeck";
	internal const string DeniedIssueId = "denied-in-mutedeck";
	internal const string ApprovalNotificationKey = "mutedeck-approval";

	private readonly IMuteDeckConnection _connection;
	private readonly HostTokenStore _tokenStore;
	private readonly PluginMetadata _metadata;
	private readonly ILogger _logger;
	private readonly IReadOnlyList<string> _iconActionIds;

	private static readonly Dictionary<string, MuteDeckStatusFields> _iconStatusFields = new(StringComparer.Ordinal)
	{
		[ToggleActions.MuteId] = MuteDeckStatusFields.Mute,
		[ToggleActions.VideoId] = MuteDeckStatusFields.Video,
		[ToggleActions.ShareId] = MuteDeckStatusFields.Share,
		[ToggleActions.RecordId] = MuteDeckStatusFields.Record,
		[LeaveMeetingAction.ActionId] = MuteDeckStatusFields.Call,
		[BringToFrontAction.ActionId] = MuteDeckStatusFields.Call
	};

	private readonly Lock _endpointGate = new();

	private volatile IIntegrationContext? _context;
	private MuteDeckEndpoint? _endpoint;
	private int _approvalShown;

	public MuteDeckIntegration(IMuteDeckConnection connection, HostTokenStore tokenStore, PluginMetadata metadata, ILogger logger)
	{
		_connection = connection;
		_tokenStore = tokenStore;
		_metadata = metadata;
		_logger = logger.ForContext<MuteDeckIntegration>();

		Actions =
		[
			ToggleActions.Mute(connection),
			ToggleActions.Video(connection),
			ToggleActions.Share(connection),
			ToggleActions.Record(connection),
			new LeaveMeetingAction(connection),
			new BringToFrontAction(connection),
			new CustomActionAction(connection)
		];
		_iconActionIds = [.. Actions.OfType<IIconProviderActionDefinition>().Cast<IActionDefinition>().Select(action => action.Id)];
		Migrations = [new MuteDeckMacroDeck2Migration(metadata.Id)];

		_connection.Changed += OnConnectionChanged;
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public IReadOnlyList<VariableDefinition> Variables => MuteDeckVariables.Definitions;

	public IReadOnlyList<IIntegrationMigration> Migrations { get; }

	public bool AllowsMultipleConfigurations => false;

	/// <summary>
	/// Runs on every connect, non-resume reconnect and configuration change. The connection is only
	/// restarted when the endpoint changed, so a pending approval in MuteDeck survives all of these.
	/// </summary>
	public async Task InitializeAsync(IIntegrationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var entries = await context.Config.GetEntriesAsync().ConfigureAwait(false);
		if (entries.Count == 0)
		{
			// The host keeps an integration with a required setup disabled until the entry exists.
			_logger.Information("MuteDeck is not set up yet; waiting for the setup to be saved");
			_tokenStore.Unbind();
			_tokenStore.ForgetMemory();
			SetEndpoint(null);
			await _connection.StopAsync().ConfigureAwait(false);
			_context = context;
			return;
		}

		var entryId = entries[0].Id;
		var endpoint = MuteDeckEndpoint.FromStored(
			await context.Config.GetStringAsync(entryId, MuteDeckSettings.HostKey).ConfigureAwait(false),
			await context.Config.GetStringAsync(entryId, MuteDeckSettings.PortKey).ConfigureAwait(false));

		if (SetEndpoint(endpoint) is { } previous && previous != endpoint)
		{
			_tokenStore.ForgetMemory();
		}

		_tokenStore.Bind(context.Config, entryId);
		_context = context;

		var client = ClientInfoFactory.Create(_metadata.Version, context.Http.UserAgent);
		await _connection.StartAsync(endpoint, client, _tokenStore, CancellationToken.None).ConfigureAwait(false);

		await PushAsync(context, new MuteDeckChangedEventArgs { ConnectionChanged = true }).ConfigureAwait(false);
	}

	public async Task ShutdownAsync()
	{
		var context = _context;
		_context = null;
		SetEndpoint(null);
		_tokenStore.Unbind();
		await _connection.StopAsync().ConfigureAwait(false);
		DismissApproval(context);
	}

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		return ValueTask.FromResult(MuteDeckVariables.Read(localId, _connection));
	}

	public IConfigFlow CreateConfigFlow()
	{
		lock (_endpointGate)
		{
			return new MuteDeckSettingsFlow(_tokenStore, _endpoint);
		}
	}

	public Task<IReadOnlyList<IntegrationIssue>> GetIssuesAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<IntegrationIssue> issues = _connection.ConnectionState switch
		{
			ConnectionState.Pending =>
			[
				new IntegrationIssue
				{
					Id = ApprovalPendingIssueId,
					Title = Strings.Issues.ApprovalPending.Title(),
					Description = Strings.Issues.ApprovalPending.Description(),
					Severity = IntegrationIssueSeverity.Warning
				}
			],
			ConnectionState.Denied =>
			[
				new IntegrationIssue
				{
					Id = DeniedIssueId,
					Title = Strings.Issues.Denied.Title(),
					Description = Strings.Issues.Denied.Description(),
					Severity = IntegrationIssueSeverity.Info
				}
			],
			_ => []
		};
		return Task.FromResult(issues);
	}

	/// <summary>Both issues are resolved in MuteDeck, not here, so there is nothing to run.</summary>
	public Task<IssueResolution> ResolveIssueAsync(string issueId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(issueId is ApprovalPendingIssueId or DeniedIssueId
			? IssueResolution.Ok()
			: IssueResolution.Failed(Strings.Issues.Unknown(issueId)));
	}

	/// <summary>A connection change can move every icon; a status change only those that follow its fields.</summary>
	internal IEnumerable<string> IconsAffectedBy(MuteDeckChangedEventArgs change)
	{
		if (change.ConnectionChanged)
		{
			return _iconActionIds;
		}

		return _iconStatusFields
			.Where(pair => (change.StatusFields & pair.Value) != 0)
			.Select(pair => pair.Key);
	}

	/// <returns>The endpoint it replaced.</returns>
	private MuteDeckEndpoint? SetEndpoint(MuteDeckEndpoint? endpoint)
	{
		lock (_endpointGate)
		{
			var previous = _endpoint;
			_endpoint = endpoint;
			return previous;
		}
	}

	// The handler only starts the work; PushAsync catches everything, so nothing escapes onto the
	// MuteDeck client's receive thread.
	private void OnConnectionChanged(object? sender, MuteDeckChangedEventArgs e)
	{
		if (_context is { } context)
		{
			_ = PushAsync(context, e);
		}
	}

	private async Task PushAsync(IIntegrationContext context, MuteDeckChangedEventArgs change)
	{
		if (change.ConnectionChanged)
		{
			UpdateApproval(context);
		}

		try
		{
			var variables = MuteDeckVariables.AffectedBy(change);
			if (variables.Count > 0)
			{
				await context.Variables.RequestDefinitionRefreshAsync(variables).ConfigureAwait(false);
			}

			foreach (var actionId in IconsAffectedBy(change))
			{
				await context.Widgets.InvalidateIconAsync(actionId).ConfigureAwait(false);
			}
		}
		catch (Exception exception) when (exception is not OutOfMemoryException)
		{
			// Both calls are hints; the host still polls at the declared intervals.
			_logger.Debug("Could not push a MuteDeck change to Macro Deck: {Message}", exception.Message);
		}
	}

	/// <summary>Mirrors the Macro Deck 2 plugin's prompt: shown while MuteDeck waits for approval, removed after.</summary>
	private void UpdateApproval(IIntegrationContext context)
	{
		if (_connection.ConnectionState == ConnectionState.Pending)
		{
			if (Interlocked.Exchange(ref _approvalShown, 1) == 0)
			{
				context.Notifications.Notify(new UserNotificationRequest
				{
					Title = DefaultCultureText.Of(Strings.Notifications.Approval.Title()),
					Message = DefaultCultureText.Of(Strings.Notifications.Approval.Message()),
					Level = UserNotificationLevel.Warning,
					Key = ApprovalNotificationKey
				});
			}

			return;
		}

		DismissApproval(context);
	}

	private void DismissApproval(IIntegrationContext? context)
	{
		if (Interlocked.Exchange(ref _approvalShown, 0) == 1)
		{
			context?.Notifications.Dismiss(ApprovalNotificationKey);
		}
	}
}
