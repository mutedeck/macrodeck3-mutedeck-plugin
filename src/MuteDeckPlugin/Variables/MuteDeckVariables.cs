using MacroDeck.Localization;
using MacroDeck.Sdk.Variables;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Variables;

/// <summary>
/// The 17 variables of the Macro Deck 2 plugin, under exactly the same names so buttons imported from
/// Macro Deck 2 keep their bindings and label templates. Values are pushed on every MuteDeck change;
/// the refresh interval is only a fallback.
/// </summary>
internal static class MuteDeckVariables
{
	private static readonly TimeSpan _fallbackRefresh = TimeSpan.FromSeconds(5);

	private static readonly Variable[] _all =
	[
		Bool("mutedeck_connected", MuteDeckStatusFields.None, Strings.Variables.Connected.Name(),
			Strings.Variables.Connected.Description(), (connection, _) => connection.IsReady),
		Bool("mutedeck_in_call", MuteDeckStatusFields.Call, Strings.Variables.InCall.Name(),
			Strings.Variables.InCall.Description(), (_, status) => status.Call == MuteDeckStatus.Active),
		Bool("mutedeck_muted", MuteDeckStatusFields.Mute, Strings.Variables.Muted.Name(),
			Strings.Variables.Muted.Description(), (_, status) => status.Mute == MuteDeckStatus.Active),
		Bool("mutedeck_video_active", MuteDeckStatusFields.Video, Strings.Variables.VideoActive.Name(),
			Strings.Variables.VideoActive.Description(), (_, status) => status.Video == MuteDeckStatus.Active),
		Bool("mutedeck_share_active", MuteDeckStatusFields.Share, Strings.Variables.ShareActive.Name(),
			Strings.Variables.ShareActive.Description(), (_, status) => status.Share == MuteDeckStatus.Active),
		Bool("mutedeck_record_active", MuteDeckStatusFields.Record, Strings.Variables.RecordActive.Name(),
			Strings.Variables.RecordActive.Description(), (_, status) => status.Record == MuteDeckStatus.Active),
		RawStatus("mutedeck_call_status", MuteDeckStatusFields.Call, Strings.Variables.CallStatus.Name(), status => status.Call),
		RawStatus("mutedeck_mute_status", MuteDeckStatusFields.Mute, Strings.Variables.MuteStatus.Name(), status => status.Mute),
		RawStatus("mutedeck_video_status", MuteDeckStatusFields.Video, Strings.Variables.VideoStatus.Name(), status => status.Video),
		RawStatus("mutedeck_share_status", MuteDeckStatusFields.Share, Strings.Variables.ShareStatus.Name(), status => status.Share),
		RawStatus("mutedeck_record_status", MuteDeckStatusFields.Record, Strings.Variables.RecordStatus.Name(), status => status.Record),
		RawStatus("mutedeck_control_status", MuteDeckStatusFields.Control, Strings.Variables.ControlStatus.Name(), status => status.Control),
		Label("mutedeck_call_label", MuteDeckStatusFields.Call, Strings.Variables.CallLabel.Name(), status => status.Call,
			Strings.VariableValues.InCall(), Strings.VariableValues.NoCall()),
		Label("mutedeck_mute_label", MuteDeckStatusFields.Mute, Strings.Variables.MuteLabel.Name(), status => status.Mute,
			Strings.VariableValues.Muted(), Strings.VariableValues.Unmuted(), Strings.VariableValues.NoMicrophone()),
		Label("mutedeck_video_label", MuteDeckStatusFields.Video, Strings.Variables.VideoLabel.Name(), status => status.Video,
			Strings.VariableValues.On(), Strings.VariableValues.Off()),
		Label("mutedeck_share_label", MuteDeckStatusFields.Share, Strings.Variables.ShareLabel.Name(), status => status.Share,
			Strings.VariableValues.On(), Strings.VariableValues.Off()),
		Label("mutedeck_record_label", MuteDeckStatusFields.Record, Strings.Variables.RecordLabel.Name(), status => status.Record,
			Strings.VariableValues.On(), Strings.VariableValues.Off())
	];

	private static readonly Dictionary<string, Variable> _byId = _all.ToDictionary(variable => variable.Id, StringComparer.Ordinal);

	public static IReadOnlyList<VariableDefinition> Definitions { get; } = [.. _all.Select(variable => variable.Definition)];

	/// <summary>
	/// Only mutedeck_connected has a value while MuteDeck is not connected: anything else would be a
	/// stale or invented reading, and Unavailable is how a provider says "no value right now".
	/// </summary>
	public static VariableReading Read(string localId, IMuteDeckConnection connection)
	{
		if (!_byId.TryGetValue(localId, out var variable))
		{
			return VariableReading.Unavailable;
		}

		if (variable.Field != MuteDeckStatusFields.None && !connection.IsReady)
		{
			return VariableReading.Unavailable;
		}

		return VariableReading.Of(variable.Read(connection, connection.Status));
	}

	/// <summary>The variable ids whose value a change can have moved.</summary>
	public static IReadOnlyList<string> AffectedBy(MuteDeckChangedEventArgs change)
	{
		if (change.ConnectionChanged)
		{
			return [.. _all.Select(variable => variable.Id)];
		}

		return
		[
			.. _all
				.Where(variable => variable.Field != MuteDeckStatusFields.None && (change.StatusFields & variable.Field) != 0)
				.Select(variable => variable.Id)
		];
	}

	private static Variable Bool(
		string name,
		MuteDeckStatusFields field,
		LocalizedText displayName,
		LocalizedText description,
		Func<IMuteDeckConnection, MuteDeckStatus, bool> read)
	{
		return Create(name, VariableType.Boolean, field, displayName, description, (connection, status) => read(connection, status));
	}

	private static Variable RawStatus(string name, MuteDeckStatusFields field, LocalizedText displayName, Func<MuteDeckStatus, string> read)
	{
		return Create(name, VariableType.Text, field, displayName, Strings.Variables.RawStatusDescription(), (_, status) => read(status));
	}

	private static Variable Label(
		string name,
		MuteDeckStatusFields field,
		LocalizedText displayName,
		Func<MuteDeckStatus, string> read,
		LocalizedText active,
		LocalizedText inactive,
		LocalizedText? disabled = null)
	{
		// Resolved once: these values are plain strings on the wire, kept identical to Macro Deck 2's.
		var activeText = DefaultCultureText.Of(active);
		var inactiveText = DefaultCultureText.Of(inactive);
		var disabledText = DefaultCultureText.Of(disabled ?? (LocalizedText)Strings.VariableValues.Disabled());

		return Create(name, VariableType.Text, field, displayName, Strings.Variables.LabelDescription(),
			(_, status) => read(status) switch
			{
				MuteDeckStatus.Active => activeText,
				MuteDeckStatus.Inactive => inactiveText,
				MuteDeckStatus.Disabled => disabledText,
				_ => string.Empty
			});
	}

	private static Variable Create(
		string name,
		VariableType type,
		MuteDeckStatusFields field,
		LocalizedText displayName,
		LocalizedText description,
		Func<IMuteDeckConnection, MuteDeckStatus, object> read)
	{
		var definition = VariableDefinition.Eager(name, type, refreshInterval: _fallbackRefresh) with
		{
			DisplayName = displayName,
			Description = description
		};
		var id = definition.ResolvedId ?? throw new InvalidOperationException($"'{name}' does not derive a variable id.");
		return new Variable(id, field, definition with { Id = id }, read);
	}

	/// <param name="Field">The status field the value follows; None for the connection itself.</param>
	private sealed record Variable(
		string Id,
		MuteDeckStatusFields Field,
		VariableDefinition Definition,
		Func<IMuteDeckConnection, MuteDeckStatus, object> Read);
}
