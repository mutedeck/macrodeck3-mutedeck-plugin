using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

/// <summary>
/// Runs one of MuteDeck's custom actions by name. The names come from MuteDeck at runtime, and a typed
/// name is accepted too, so a button can be set up before MuteDeck is running.
/// </summary>
internal sealed class CustomActionAction(IMuteDeckConnection connection)
	: IDynamicOptionsActionDefinition, IIconProviderActionDefinition
{
	public const string ActionId = "custom-action";
	public const string NameParameter = "name";

	private const int MaxOptions = 200;

	public string Id => ActionId;

	public LocalizedText Name => Strings.Actions.CustomAction.Name();

	public LocalizedText Description => Strings.Actions.CustomAction.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Autocomplete(NameParameter,
			label: Strings.Actions.CustomAction.ActionName.Label(),
			description: Strings.Actions.CustomAction.ActionName.Description(),
			placeholder: Strings.Actions.CustomAction.ActionName.Placeholder(),
			required: true)
	];

	public IActionExecutor CreateExecutor() => new Executor(connection);

	public async Task<DynamicOptionsResult> GetDynamicOptionsAsync(
		DynamicOptionsContext context,
		CancellationToken cancellationToken)
	{
		if (!connection.IsReady)
		{
			return new DynamicOptionsResult { Options = [], AllowsCustomValue = true, Error = Strings.Results.NotConnected() };
		}

		if (connection.CustomActions.Count == 0)
		{
			await connection.RefreshCustomActionsAsync(cancellationToken).ConfigureAwait(false);
		}

		var filter = context.Filter?.Trim();
		var options = connection.CustomActions
			.Where(name => string.IsNullOrEmpty(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase))
			.Take(MaxOptions)
			.Select(name => new ActionParameterOption { Value = name, Label = name })
			.ToList();

		// Short, because MuteDeck pushes changes to its list and the picker should follow them.
		return new DynamicOptionsResult { Options = options, AllowsCustomValue = true, CacheSeconds = 10 };
	}

	public Task<ActionIconSnapshot?> GetActionIconAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		if (!connection.IsReady)
		{
			return MuteDeckIcons.Snapshot(null);
		}

		var name = parameters.TryGetValue(NameParameter, out var value) ? value?.ToString() : null;
		return MuteDeckIcons.Snapshot(string.IsNullOrWhiteSpace(name) ? MuteDeckIcons.CustomAction : MuteDeckIcons.ForCustomAction(name));
	}

	private sealed class Executor(IMuteDeckConnection connection) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var name = context.Parameters.TryGetValue(NameParameter, out var value) ? value?.ToString()?.Trim() : null;
			if (string.IsNullOrEmpty(name))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.CustomAction.ActionName.Label()));
			}

			if (!connection.IsReady)
			{
				return SendResults.NotConnected();
			}

			return SendResults.ToActionResult(await connection.CustomActionAsync(name, context.CancellationToken).ConfigureAwait(false));
		}
	}
}
