using System.Globalization;

namespace MuteDeckPlugin.Connection;

internal readonly record struct MuteDeckEndpoint(string Host, int Port)
{
	public const string DefaultHost = "localhost";
	public const int DefaultPort = 3492;

	public static MuteDeckEndpoint Default { get; } = new(DefaultHost, DefaultPort);

	/// <summary>
	/// Reads stored values leniently: the host returns a number field as its raw JSON text, and a value
	/// imported from Macro Deck 2 may be blank, so anything unusable falls back to the default.
	/// </summary>
	public static MuteDeckEndpoint FromStored(string? host, string? port)
	{
		return new MuteDeckEndpoint(
			IsValidHost(host) ? host!.Trim() : DefaultHost,
			TryParsePort(port, out var parsed) ? parsed : DefaultPort);
	}

	public static bool IsValidHost(string? host)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return false;
		}

		var trimmed = host.Trim();
		return !trimmed.Contains("://", StringComparison.Ordinal)
			&& Uri.CheckHostName(trimmed) != UriHostNameType.Unknown;
	}

	public static bool TryParsePort(string? value, out int port)
	{
		port = 0;
		if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
			|| number != Math.Floor(number)
			|| number is < 1 or > 65535)
		{
			return false;
		}

		port = (int)number;
		return true;
	}

	public override string ToString() => $"{Host}:{Port.ToString(CultureInfo.InvariantCulture)}";
}
