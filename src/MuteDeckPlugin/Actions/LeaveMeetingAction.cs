using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

internal sealed class LeaveMeetingAction(IMuteDeckConnection connection)
	: IActionDefinition, IStateProviderActionDefinition, IIconProviderActionDefinition
{
	public const string ActionId = "leave-meeting";

	private static readonly IReadOnlyList<ActionStateDefinition> _states =
		[ActionStates.InCall, ActionStates.NoCall, ActionStates.Unavailable];

	public string Id => ActionId;

	public LocalizedText Name => Strings.Actions.LeaveMeeting.Name();

	public LocalizedText Description => Strings.Actions.LeaveMeeting.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(connection);

	public Task<ActionStateSnapshot?> GetActionStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		var stateId = CallStatus(connection) switch
		{
			MuteDeckStatus.Active => ActionStates.InCall.Id,
			MuteDeckStatus.Inactive or MuteDeckStatus.Disabled => ActionStates.NoCall.Id,
			_ => ActionStates.UnavailableId
		};
		return Task.FromResult<ActionStateSnapshot?>(new ActionStateSnapshot(_states, stateId));
	}

	public Task<ActionIconSnapshot?> GetActionIconAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		return MuteDeckIcons.Snapshot(CallStatus(connection) switch
		{
			MuteDeckStatus.Active => MuteDeckIcons.LeaveMeeting,
			MuteDeckStatus.Inactive or MuteDeckStatus.Disabled => MuteDeckIcons.LeaveMeetingDisabled,
			_ => null
		});
	}

	internal static string CallStatus(IMuteDeckConnection connection)
	{
		return connection.IsReady ? connection.Status.Call : string.Empty;
	}

	private sealed class Executor(IMuteDeckConnection connection) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (!connection.IsReady)
			{
				return SendResults.NotConnected();
			}

			var result = await connection.LeaveMeetingAsync(context.CancellationToken).ConfigureAwait(false);
			return SendResults.ToActionResult(result, result.Outcome == MuteDeckSendOutcome.Succeeded ? ActionStates.NoCall.Id : null);
		}
	}
}
