using MacroDeck.Sdk.Actions;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Actions;

internal static class SendResults
{
	public static ActionResult ToActionResult(MuteDeckSendResult result, string? expectedStateId = null)
	{
		return result.Outcome switch
		{
			MuteDeckSendOutcome.Succeeded => expectedStateId is null
				? ActionResult.Success()
				: ActionResult.Success(expectedStateId),
			MuteDeckSendOutcome.SentUnconfirmed => expectedStateId is null
				? ActionResult.Accepted(Strings.Results.NotConfirmed())
				: ActionResult.Accepted(Strings.Results.NotConfirmed(), expectedStateId),
			MuteDeckSendOutcome.Rejected => Rejected(result.Reason),
			MuteDeckSendOutcome.NotConnected => NotConnected(),
			MuteDeckSendOutcome.Timeout => ActionResult.Failed(ActionErrorCodes.Timeout, Strings.Results.Timeout()),
			_ => ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Results.Failed())
		};
	}

	public static ActionResult NotConnected()
	{
		return ActionResult.Failed(ActionErrorCodes.NotConnected, Strings.Results.NotConnected());
	}

	private static ActionResult Rejected(string? reason)
	{
		return reason switch
		{
			"not_in_call" => ActionResult.Failed(ActionErrorCodes.ProviderRejected, Strings.Results.NotInCall()),
			"not_licensed" => ActionResult.Failed(ActionErrorCodes.PermissionDenied, Strings.Results.NotLicensed()),
			"unknown_action" => ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Results.UnknownAction()),
			_ => ActionResult.Failed(ActionErrorCodes.ProviderRejected, Strings.Results.Rejected(reason ?? "unknown"))
		};
	}
}
