using System.Text.Json;
using MacroDeck.Localization;
using MacroDeck.Sdk.Migration;
using MuteDeckPlugin.Actions;
using MuteDeckPlugin.Connection;
using MuteDeckPlugin.Setup;

namespace MuteDeckPlugin.Migration;

/// <summary>
/// Takes over the Macro Deck 2 MuteDeck plugin's buttons, settings and approval token, so a user who
/// moves to Macro Deck 3 keeps their deck and does not have to approve Macro Deck in MuteDeck again.
/// </summary>
internal sealed class MuteDeckMacroDeck2Migration(string integrationId) : IIntegrationMigration
{
	private const string ActionNamespace = "MuteDeck.Plugin.Actions.";

	public MigrationSource Source => MigrationSource.MacroDeck2;

	// The Macro Deck 2 plugin's AssemblyName.
	public IReadOnlyList<string> ClaimedActionSources { get; } = ["MuteDeckPlugin"];

	// Macro Deck 2 names a plugin's settings and credentials files "<author>_<name>" in lowercase. The
	// name is the manifest's "MuteDeck"; the assembly name is claimed too in case a build used it.
	public IReadOnlyList<string> ClaimedSettingsSources { get; } = ["mutedeck_mutedeck", "mutedeck_mutedeckplugin"];

	public Task<ActionMigrationResult?> MigrateActionAsync(ForeignAction action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		return Task.FromResult(action.TypeName switch
		{
			ActionNamespace + "ToggleMuteAction" => Toggle(action, ToggleActions.MuteId, Strings.Actions.ToggleMute.Name()),
			ActionNamespace + "ToggleVideoAction" => Toggle(action, ToggleActions.VideoId, Strings.Actions.ToggleVideo.Name()),
			ActionNamespace + "ToggleShareAction" => Toggle(action, ToggleActions.ShareId, Strings.Actions.ToggleShare.Name()),
			ActionNamespace + "ToggleRecordAction" => Toggle(action, ToggleActions.RecordId, Strings.Actions.ToggleRecord.Name()),
			ActionNamespace + "LeaveMeetingAction" => Result(action, LeaveMeetingAction.ActionId, Strings.Actions.LeaveMeeting.Name(), []),
			ActionNamespace + "BringToFrontAction" => Result(action, BringToFrontAction.ActionId, Strings.Actions.BringToFront.Name(), []),
			ActionNamespace + "CustomActionAction" => CustomAction(action),
			_ => null
		});
	}

	public Task<IReadOnlyList<MigratedConfiguration>> MigrateConfigurationAsync(
		ForeignPluginSettings settings,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);

		// The Macro Deck 2 plugin worked with no settings at all, so an entry with the defaults is a
		// working configuration, not one that only looks configured.
		var endpoint = MuteDeckEndpoint.FromStored(
			settings.Settings.GetValueOrDefault(MuteDeckSettings.HostKey),
			settings.Settings.GetValueOrDefault(MuteDeckSettings.PortKey));

		var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
		{
			[MuteDeckSettings.HostKey] = JsonSerializer.SerializeToElement(endpoint.Host),
			[MuteDeckSettings.PortKey] = JsonSerializer.SerializeToElement(endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))
		};

		var secrets = new Dictionary<string, MigratedSecret>(StringComparer.Ordinal);
		var credentials = settings.Credentials.Count > 0 ? settings.Credentials[0] : null;
		if (credentials is not null)
		{
			foreach (var key in new[] { MuteDeckSettings.TokenKey, MuteDeckSettings.InstanceIdKey })
			{
				if (credentials.TryGetValue(key, out var secret) && !string.IsNullOrEmpty(secret))
				{
					secrets[key] = new MigratedSecret(secret, MigratedSecretKind.Secret);
				}
			}
		}

		return Task.FromResult<IReadOnlyList<MigratedConfiguration>>(
			[new MigratedConfiguration(integrationId, MuteDeckSettings.EntryTitle, values, secrets)]);
	}

	private ActionMigrationResult? Toggle(ForeignAction action, string actionId, LocalizedText defaultLabel)
	{
		// Macro Deck 2 stored {"State":"toggle|on|off"}; an empty configuration was its default, toggle.
		if (!TryParse(action.Configuration, out var config))
		{
			return null;
		}

		var state = ReadString(config, "State");
		state = string.IsNullOrWhiteSpace(state) ? ToggleAction.Toggle : state.Trim();
		var known = new[] { ToggleAction.Toggle, ToggleAction.On, ToggleAction.Off }
			.FirstOrDefault(choice => string.Equals(choice, state, StringComparison.OrdinalIgnoreCase));
		return known is null
			? null
			: Result(action, actionId, defaultLabel, [(ToggleAction.StateParameter, known)]);
	}

	private ActionMigrationResult? CustomAction(ForeignAction action)
	{
		// A button without a name did nothing in Macro Deck 2; left as a placeholder rather than turned
		// into an action that fails on every press.
		if (!TryParse(action.Configuration, out var config) || ReadString(config, "Name") is not { Length: > 0 } name
			|| string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		return Result(action, CustomActionAction.ActionId, Strings.Actions.CustomAction.Name(),
			[(CustomActionAction.NameParameter, name.Trim())]);
	}

	private ActionMigrationResult Result(
		ForeignAction action,
		string actionId,
		LocalizedText defaultLabel,
		IReadOnlyList<(string Name, string Value)> parameters)
	{
		return new ActionMigrationResult(integrationId,
			actionId,
			string.IsNullOrWhiteSpace(action.DisplayName) ? DefaultCultureText.Of(defaultLabel) : action.DisplayName,
			parameters.ToDictionary(parameter => parameter.Name,
				parameter => JsonSerializer.SerializeToElement(parameter.Value),
				StringComparer.Ordinal));
	}

	private static bool TryParse(string? configuration, out JsonElement config)
	{
		try
		{
			using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(configuration) ? "{}" : configuration);
			config = document.RootElement.Clone();
			return config.ValueKind == JsonValueKind.Object;
		}
		catch (JsonException)
		{
			config = default;
			return false;
		}
	}

	private static string? ReadString(JsonElement config, string name)
	{
		foreach (var property in config.EnumerateObject())
		{
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
			}
		}

		return null;
	}
}
