namespace MuteDeckPlugin.Setup;

/// <summary>
/// Keys of the integration's one config entry. They match the Macro Deck 2 plugin's settings and
/// credential names, so a migrated entry needs no renaming.
/// </summary>
internal static class MuteDeckSettings
{
	public const string HostKey = "host";
	public const string PortKey = "port";
	public const string TokenKey = "mutedeck_token";
	public const string InstanceIdKey = "mutedeck_instance_id";

	/// <summary>The config entry's name. A plain string by contract: the user renames it in Macro Deck.</summary>
	public const string EntryTitle = "MuteDeck";
}
