namespace MuteDeckPlugin.Connection;

/// <summary>
/// The six status fields MuteDeck reports. Each is "active", "inactive", "disabled", or empty while
/// nothing has been received yet (Control names the controlled app instead).
/// </summary>
internal sealed record MuteDeckStatus(string Call, string Mute, string Video, string Share, string Record, string Control)
{
	public const string Active = "active";
	public const string Inactive = "inactive";
	public const string Disabled = "disabled";

	public static MuteDeckStatus Empty { get; } = new("", "", "", "", "", "");

	public bool InCall => Call == Active;

	public MuteDeckStatusFields DifferencesFrom(MuteDeckStatus other)
	{
		var changed = MuteDeckStatusFields.None;
		if (Call != other.Call)
		{
			changed |= MuteDeckStatusFields.Call;
		}

		if (Mute != other.Mute)
		{
			changed |= MuteDeckStatusFields.Mute;
		}

		if (Video != other.Video)
		{
			changed |= MuteDeckStatusFields.Video;
		}

		if (Share != other.Share)
		{
			changed |= MuteDeckStatusFields.Share;
		}

		if (Record != other.Record)
		{
			changed |= MuteDeckStatusFields.Record;
		}

		if (Control != other.Control)
		{
			changed |= MuteDeckStatusFields.Control;
		}

		return changed;
	}
}

[Flags]
internal enum MuteDeckStatusFields
{
	None = 0,
	Call = 1,
	Mute = 2,
	Video = 4,
	Share = 8,
	Record = 16,
	Control = 32,
	All = Call | Mute | Video | Share | Record | Control
}
