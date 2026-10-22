using MuteDeckPlugin.Connection;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class ClientInfoTests
{
	[TestCase("MacroDeck/3.0.0-beta.18", "3.0.0-beta.18")]
	[TestCase("MacroDeck/3.1.0 (macOS)", "3.1.0")]
	[TestCase("MyCustomAgent/1.0", null)]
	[TestCase("", null)]
	[TestCase(null, null)]
	public void The_host_version_is_read_from_the_default_user_agent_only(string? userAgent, string? expected)
	{
		Assert.That(ClientInfoFactory.HostVersionFrom(userAgent), Is.EqualTo(expected));
	}

	[Test]
	public void The_operating_system_is_the_real_one()
	{
		var expected = OperatingSystem.IsMacOS() ? "mac" : OperatingSystem.IsWindows() ? "windows" : "linux";

		Assert.That(ClientInfoFactory.Create("1.0.0", null).Os, Is.EqualTo(expected));
	}

	[TestCase("localhost", true)]
	[TestCase("192.168.1.20", true)]
	[TestCase("studio-pc.local", true)]
	[TestCase("http://localhost", false)]
	[TestCase("local host", false)]
	[TestCase("", false)]
	public void Hosts_are_validated(string host, bool valid)
	{
		Assert.That(MuteDeckEndpoint.IsValidHost(host), Is.EqualTo(valid));
	}
}
