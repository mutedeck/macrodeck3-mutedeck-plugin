using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

/// <summary>Everything that tells the four MuteDeck toggles apart.</summary>
internal sealed record ToggleSpec
{
	public required string Id { get; init; }

	/// <summary>MuteDeck's action name, such as toggle_mute.</summary>
	public required string WireAction { get; init; }

	public required LocalizedText Name { get; init; }

	public required LocalizedText Description { get; init; }

	/// <summary>Label of the "on" choice, such as Mute or Start camera.</summary>
	public required LocalizedText OnLabel { get; init; }

	public required LocalizedText OffLabel { get; init; }

	public required Func<MuteDeckStatus, string> Read { get; init; }

	public required ActionStateDefinition ActiveState { get; init; }

	public required ActionStateDefinition InactiveState { get; init; }

	public required ActionStateDefinition DisabledState { get; init; }

	public required string ActiveIcon { get; init; }

	public required string InactiveIcon { get; init; }

	public required string DisabledIcon { get; init; }
}

/// <summary>
/// Microphone, camera, screen share or recording: toggles, or sets on or off, and reports the live
/// state and icon of what it controls.
/// </summary>
internal sealed class ToggleAction : IActionDefinition, IStateProviderActionDefinition, IIconProviderActionDefinition
{
	public const string StateParameter = "state";
	public const string Toggle = "toggle";
	public const string On = "on";
	public const string Off = "off";

	private readonly ToggleSpec _spec;
	private readonly IMuteDeckConnection _connection;
	private readonly IReadOnlyList<ActionStateDefinition> _states;

	public ToggleAction(ToggleSpec spec, IMuteDeckConnection connection)
	{
		_spec = spec;
		_connection = connection;
		_states = [spec.ActiveState, spec.InactiveState, spec.DisabledState, ActionStates.Unavailable];
		Parameters =
		[
			ActionParameter.Choice(StateParameter,
				[
					new ActionParameterOption { Value = Toggle, Label = Strings.Actions.ToggleState.Toggle() },
					new ActionParameterOption { Value = On, Label = spec.OnLabel },
					new ActionParameterOption { Value = Off, Label = spec.OffLabel }
				],
				label: Strings.Actions.ToggleState.Label(),
				description: Strings.Actions.ToggleState.Description(),
				defaultValue: Toggle,
				required: true)
		];
	}

	public string Id => _spec.Id;

	public LocalizedText Name => _spec.Name;

	public LocalizedText Description => _spec.Description;

	public IReadOnlyList<ActionParameter> Parameters { get; }

	public string WireAction => _spec.WireAction;

	public IActionExecutor CreateExecutor() => new Executor(this);

	public Task<ActionStateSnapshot?> GetActionStateAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		var stateId = (_connection.IsReady ? _spec.Read(_connection.Status) : string.Empty) switch
		{
			MuteDeckStatus.Active => _spec.ActiveState.Id,
			MuteDeckStatus.Inactive => _spec.InactiveState.Id,
			MuteDeckStatus.Disabled => _spec.DisabledState.Id,
			_ => ActionStates.UnavailableId
		};
		return Task.FromResult<ActionStateSnapshot?>(new ActionStateSnapshot(_states, stateId));
	}

	public Task<ActionIconSnapshot?> GetActionIconAsync(
		IReadOnlyDictionary<string, object?> parameters,
		CancellationToken cancellationToken)
	{
		var icon = (_connection.IsReady ? _spec.Read(_connection.Status) : string.Empty) switch
		{
			MuteDeckStatus.Active => _spec.ActiveIcon,
			MuteDeckStatus.Inactive => _spec.InactiveIcon,
			MuteDeckStatus.Disabled => _spec.DisabledIcon,
			_ => null
		};
		return MuteDeckIcons.Snapshot(icon);
	}

	/// <summary>The state a press should end in, so the button can show it before MuteDeck confirms.</summary>
	private string? ExpectedStateAfter(string requested)
	{
		var current = _spec.Read(_connection.Status);
		return requested switch
		{
			On => _spec.ActiveState.Id,
			Off => _spec.InactiveState.Id,
			_ when current == MuteDeckStatus.Active => _spec.InactiveState.Id,
			_ when current == MuteDeckStatus.Inactive => _spec.ActiveState.Id,
			_ => null
		};
	}

	/// <summary>A blank value is the default, toggle; anything unknown is null.</summary>
	private static string? Normalize(string? value)
	{
		var trimmed = value?.Trim();
		if (string.IsNullOrEmpty(trimmed))
		{
			return Toggle;
		}

		return new[] { Toggle, On, Off }.FirstOrDefault(choice => string.Equals(choice, trimmed, StringComparison.OrdinalIgnoreCase));
	}

	private sealed class Executor(ToggleAction action) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			var requested = Normalize(context.Parameters.TryGetValue(StateParameter, out var value) ? value?.ToString() : null);
			if (requested is null)
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.InvalidValue(Strings.Actions.ToggleState.Label()));
			}

			if (!action._connection.IsReady)
			{
				return SendResults.NotConnected();
			}

			var expected = action.ExpectedStateAfter(requested);
			var result = await action._connection.ToggleAsync(action.WireAction, requested, context.CancellationToken)
				.ConfigureAwait(false);
			return SendResults.ToActionResult(result, expected);
		}
	}
}
