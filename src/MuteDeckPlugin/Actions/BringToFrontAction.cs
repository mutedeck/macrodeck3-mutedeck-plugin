using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

internal sealed class BringToFrontAction(IMuteDeckConnection connection) : IActionDefinition, IIconProviderActionDefinition
{
	public const string ActionId = "bring-to-front";

	public string Id => ActionId;

	public LocalizedText Name => Strings.Actions.BringToFront.Name();

	public LocalizedText Description => Strings.Actions.BringToFront.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(connection);

	public Task<ActionIconSnapshot?> GetActionIconAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		return MuteDeckIcons.Snapshot(LeaveMeetingAction.CallStatus(connection) switch
		{
			MuteDeckStatus.Active => MuteDeckIcons.BringToFront,
			MuteDeckStatus.Inactive or MuteDeckStatus.Disabled => MuteDeckIcons.BringToFrontDisabled,
			_ => null
		});
	}

	private sealed class Executor(IMuteDeckConnection connection) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (!connection.IsReady)
			{
				return SendResults.NotConnected();
			}

			return SendResults.ToActionResult(await connection.BringToFrontAsync(context.CancellationToken).ConfigureAwait(false));
		}
	}
}
