namespace MuteDeckPlugin.Connection;

internal enum MuteDeckSendOutcome
{
	Succeeded,

	/// <summary>Sent to an older MuteDeck that does not answer actions, so nothing confirms it.</summary>
	SentUnconfirmed,

	Rejected,
	NotConnected,
	Timeout,
	Failed
}

/// <param name="Reason">MuteDeck's machine-readable reason for a rejection, such as not_in_call.</param>
internal sealed record MuteDeckSendResult(MuteDeckSendOutcome Outcome, string? Reason = null)
{
	public static MuteDeckSendResult Succeeded { get; } = new(MuteDeckSendOutcome.Succeeded);

	public static MuteDeckSendResult SentUnconfirmed { get; } = new(MuteDeckSendOutcome.SentUnconfirmed);

	public static MuteDeckSendResult NotConnected { get; } = new(MuteDeckSendOutcome.NotConnected);

	public static MuteDeckSendResult Timeout { get; } = new(MuteDeckSendOutcome.Timeout);

	public static MuteDeckSendResult Failed { get; } = new(MuteDeckSendOutcome.Failed);

	public static MuteDeckSendResult Rejected(string? reason) => new(MuteDeckSendOutcome.Rejected, reason);
}
