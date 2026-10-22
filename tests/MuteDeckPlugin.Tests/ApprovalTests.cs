using MacroDeck.Sdk.Issues;
using MuteDeck.PluginClient;
using NUnit.Framework;

namespace MuteDeckPlugin.Tests;

[TestFixture]
public sealed class ApprovalTests
{
	[Test]
	public async Task A_pending_approval_is_an_issue_and_a_notification()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		plugin.Connection.SetState(ConnectionState.Pending);

		var issues = await plugin.Integration.GetIssuesAsync();
		Assert.That(issues.Select(issue => issue.Id), Is.EqualTo(new[] { "approve-in-mutedeck" }));
		Assert.That(issues[0].Severity, Is.EqualTo(IntegrationIssueSeverity.Warning));
		var notification = plugin.Harness.Context.Notifications.Current.Single();
		Assert.That(notification.Key, Is.EqualTo("mutedeck-approval"));
		Assert.That(notification.Message, Is.EqualTo("Approve Macro Deck in MuteDeck to control your meetings."));
	}

	[Test]
	public async Task Approving_removes_the_issue_and_the_notification()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();
		plugin.Connection.SetState(ConnectionState.Pending);

		plugin.Connection.SetState(ConnectionState.Authenticated);

		Assert.That(await plugin.Integration.GetIssuesAsync(), Is.Empty);
		Assert.That(plugin.Harness.Context.Notifications.Current, Is.Empty);
	}

	[Test]
	public async Task A_denial_is_reported_as_information()
	{
		await using var plugin = await TestPlugin.Create().InitializeConfiguredAsync();

		plugin.Connection.SetState(ConnectionState.Denied);

		var issue = (await plugin.Integration.GetIssuesAsync()).Single();
		Assert.That(issue.Id, Is.EqualTo("denied-in-mutedeck"));
		Assert.That(issue.Severity, Is.EqualTo(IntegrationIssueSeverity.Info));
	}

	[Test]
	public async Task An_unknown_issue_cannot_be_resolved()
	{
		await using var plugin = TestPlugin.Create();

		var resolution = await plugin.Integration.ResolveIssueAsync("something-else");

		Assert.That(resolution.Success, Is.False);
	}
}
