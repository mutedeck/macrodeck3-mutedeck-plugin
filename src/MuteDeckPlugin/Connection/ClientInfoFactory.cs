using System.Text.RegularExpressions;
using MuteDeck.PluginClient;

namespace MuteDeckPlugin.Connection;

/// <summary>What MuteDeck shows in its approval prompt and records as the controlling app.</summary>
internal static partial class ClientInfoFactory
{
	public const string ClientId = "macro-deck";
	public const string ClientName = "Macro Deck";
	public const string HostApp = "macro-deck";

	public static ClientInfo Create(string pluginVersion, string? hostUserAgent)
	{
		return new ClientInfo
		{
			Id = ClientId,
			Name = ClientName,
			Version = ClientInfo.SanitizeVersion(pluginVersion),
			HostApp = HostApp,
			HostVersion = HostVersionFrom(hostUserAgent),
			Os = CurrentOs()
		};
	}

	/// <summary>
	/// The host's version is only exposed through its User-Agent, "MacroDeck/&lt;version&gt;" by default. The
	/// user can override that value, so anything else yields no version rather than a wrong one.
	/// </summary>
	public static string? HostVersionFrom(string? userAgent)
	{
		if (string.IsNullOrWhiteSpace(userAgent))
		{
			return null;
		}

		var match = HostVersionPattern().Match(userAgent);
		return match.Success ? ClientInfo.SanitizeVersion(match.Groups["version"].Value) : null;
	}

	/// <summary>The values MuteDeck's protocol defines for ClientInfo.Os.</summary>
	public static string? CurrentOs()
	{
		if (OperatingSystem.IsMacOS())
		{
			return "mac";
		}

		if (OperatingSystem.IsWindows())
		{
			return "windows";
		}

		return OperatingSystem.IsLinux() ? "linux" : null;
	}

	[GeneratedRegex(@"^MacroDeck/(?<version>[0-9][^\s]*)", RegexOptions.CultureInvariant)]
	private static partial Regex HostVersionPattern();
}
