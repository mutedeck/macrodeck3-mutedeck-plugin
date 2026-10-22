using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace MuteDeckPlugin.Actions;

/// <summary>
/// State definitions shared by the actions. Ids are persisted in the user's buttons once adopted, so
/// they never change. Colors are only a starting point the user restyles in the button editor.
/// </summary>
internal static class ActionStates
{
	public const string UnavailableId = "unavailable";

	private const string Red = "#c53030";
	private const string Green = "#2f855a";
	private const string Grey = "#4a5568";
	private const string MutedText = "#cbd5e0";

	public static ActionStateDefinition Unavailable { get; } = Define(UnavailableId, MacroDeckStrings.States.Unavailable(), Grey, MutedText);

	public static ActionStateDefinition Muted { get; } = Define("muted", MacroDeckStrings.States.Muted(), Red);

	public static ActionStateDefinition Unmuted { get; } = Define("unmuted", MacroDeckStrings.States.Unmuted(), Green);

	public static ActionStateDefinition NoMicrophone { get; } = Define("no-mic", Strings.States.NoMicrophone(), Grey, MutedText);

	public static ActionStateDefinition InCall { get; } = Define("in-call", Strings.States.InCall(), Red);

	public static ActionStateDefinition NoCall { get; } = Define("no-call", Strings.States.NoCall(), Grey, MutedText);

	public static ActionStateDefinition On(string onColor) => Define("on", MacroDeckStrings.States.On(), onColor);

	public static ActionStateDefinition Off(string offColor) => Define("off", MacroDeckStrings.States.Off(), offColor);

	public static ActionStateDefinition Disabled { get; } = Define("disabled", MacroDeckStrings.Common.Disabled(), Grey, MutedText);

	public static string OnColor(bool onIsAlert) => onIsAlert ? Red : Green;

	public static string OffColor(bool offIsAlert) => offIsAlert ? Red : Grey;

	private static ActionStateDefinition Define(string id, LocalizedText label, string background, string? labelColor = null)
	{
		return new ActionStateDefinition(id, label)
		{
			DefaultAppearance = new ActionStateAppearance { BackgroundColor = background, LabelColor = labelColor }
		};
	}
}
