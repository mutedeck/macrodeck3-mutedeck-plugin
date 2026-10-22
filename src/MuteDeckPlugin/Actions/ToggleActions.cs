using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

internal static class ToggleActions
{
	public const string MuteId = "toggle-mute";
	public const string VideoId = "toggle-video";
	public const string ShareId = "toggle-share";
	public const string RecordId = "toggle-record";

	public static ToggleAction Mute(IMuteDeckConnection connection) => new(new ToggleSpec
	{
		Id = MuteId,
		WireAction = "toggle_mute",
		Name = Strings.Actions.ToggleMute.Name(),
		Description = Strings.Actions.ToggleMute.Description(),
		OnLabel = Strings.Actions.ToggleMute.On(),
		OffLabel = Strings.Actions.ToggleMute.Off(),
		Read = status => status.Mute,
		ActiveState = ActionStates.Muted,
		InactiveState = ActionStates.Unmuted,
		DisabledState = ActionStates.NoMicrophone,
		ActiveIcon = MuteDeckIcons.Muted,
		InactiveIcon = MuteDeckIcons.Unmuted,
		DisabledIcon = MuteDeckIcons.MicrophoneDisabled
	}, connection);

	// A stopped camera is the one "off" a meeting app shows as a warning, so it starts out red.
	public static ToggleAction Video(IMuteDeckConnection connection) => new(new ToggleSpec
	{
		Id = VideoId,
		WireAction = "toggle_video",
		Name = Strings.Actions.ToggleVideo.Name(),
		Description = Strings.Actions.ToggleVideo.Description(),
		OnLabel = Strings.Actions.ToggleVideo.On(),
		OffLabel = Strings.Actions.ToggleVideo.Off(),
		Read = status => status.Video,
		ActiveState = ActionStates.On(ActionStates.OnColor(onIsAlert: false)),
		InactiveState = ActionStates.Off(ActionStates.OffColor(offIsAlert: true)),
		DisabledState = ActionStates.Disabled,
		ActiveIcon = MuteDeckIcons.VideoStarted,
		InactiveIcon = MuteDeckIcons.VideoStopped,
		DisabledIcon = MuteDeckIcons.VideoDisabled
	}, connection);

	public static ToggleAction Share(IMuteDeckConnection connection) => new(new ToggleSpec
	{
		Id = ShareId,
		WireAction = "toggle_share",
		Name = Strings.Actions.ToggleShare.Name(),
		Description = Strings.Actions.ToggleShare.Description(),
		OnLabel = Strings.Actions.ToggleShare.On(),
		OffLabel = Strings.Actions.ToggleShare.Off(),
		Read = status => status.Share,
		ActiveState = ActionStates.On(ActionStates.OnColor(onIsAlert: false)),
		InactiveState = ActionStates.Off(ActionStates.OffColor(offIsAlert: false)),
		DisabledState = ActionStates.Disabled,
		ActiveIcon = MuteDeckIcons.SharingStarted,
		InactiveIcon = MuteDeckIcons.SharingStopped,
		DisabledIcon = MuteDeckIcons.SharingDisabled
	}, connection);

	// A running recording is what the user must not miss, so "on" starts out red.
	public static ToggleAction Record(IMuteDeckConnection connection) => new(new ToggleSpec
	{
		Id = RecordId,
		WireAction = "toggle_record",
		Name = Strings.Actions.ToggleRecord.Name(),
		Description = Strings.Actions.ToggleRecord.Description(),
		OnLabel = Strings.Actions.ToggleRecord.On(),
		OffLabel = Strings.Actions.ToggleRecord.Off(),
		Read = status => status.Record,
		ActiveState = ActionStates.On(ActionStates.OnColor(onIsAlert: true)),
		InactiveState = ActionStates.Off(ActionStates.OffColor(offIsAlert: false)),
		DisabledState = ActionStates.Disabled,
		ActiveIcon = MuteDeckIcons.RecordingStarted,
		InactiveIcon = MuteDeckIcons.RecordingStopped,
		DisabledIcon = MuteDeckIcons.RecordingDisabled
	}, connection);
}
