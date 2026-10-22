using MacroDeck.Sdk.Actions;

namespace MuteDeckPlugin.Actions;

/// <summary>
/// Icons of the bundled pack, addressed by the pack key in manifest.json and the icon's name, which is
/// the source PNG's file name in icon-src/.
/// </summary>
internal static class MuteDeckIcons
{
	public const string PackKey = "mutedeck";

	public const string Muted = "muted";
	public const string Unmuted = "unmuted";
	public const string MicrophoneDisabled = "microphone-disabled";
	public const string VideoStarted = "video-started";
	public const string VideoStopped = "video-stopped";
	public const string VideoDisabled = "video-disabled";
	public const string SharingStarted = "sharing-started";
	public const string SharingStopped = "sharing-stopped";
	public const string SharingDisabled = "sharing-disabled";
	public const string RecordingStarted = "recording-started";
	public const string RecordingStopped = "recording-stopped";
	public const string RecordingDisabled = "recording-disabled";
	public const string LeaveMeeting = "leave-meeting";
	public const string LeaveMeetingDisabled = "leave-meeting-disabled";
	public const string BringToFront = "bring-to-front";
	public const string BringToFrontDisabled = "bring-to-front-disabled";
	public const string CustomAction = "custom-action";

	/// <summary>Icons named after a MuteDeck custom action, shown instead of the generic one.</summary>
	public static IReadOnlySet<string> CustomActionIcons => _customActionIcons;

	private static readonly HashSet<string> _customActionIcons = new(StringComparer.OrdinalIgnoreCase)
	{
		"background-blur",
		"live-caption",
		"raise-hand",
		"react-applause",
		"react-clap",
		"react-heart",
		"react-laugh",
		"react-like",
		"react-love",
		"react-tada",
		"react-thumbs-up",
		"react-wow",
		"toggle-chat",
		"toggle-participants"
	};

	public static string ForCustomAction(string name)
	{
		var trimmed = name.Trim();
		return _customActionIcons.TryGetValue(trimmed, out var icon) ? icon : CustomAction;
	}

	/// <summary>Null hands the button back its own icon, which is right whenever MuteDeck has nothing to say.</summary>
	public static Task<ActionIconSnapshot?> Snapshot(string? name)
	{
		return Task.FromResult<ActionIconSnapshot?>(name is null
			? null
			: new ActionIconSnapshot
			{
				// The icon name doubles as the version: the host only re-renders when it changes.
				Version = name,
				Reference = ActionIconReference.PluginIcon(PackKey, name)
			});
	}
}
