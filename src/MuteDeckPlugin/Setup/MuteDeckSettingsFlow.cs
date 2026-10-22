using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MuteDeckPlugin.Connection;

namespace MuteDeckPlugin.Setup;

/// <summary>
/// One step with MuteDeck's host and port, prefilled with the defaults so a user on one computer only
/// has to save. Saving creates the config entry the approval token is then stored in.
/// </summary>
internal sealed class MuteDeckSettingsFlow(HostTokenStore tokenStore, MuteDeckEndpoint? currentEndpoint) : IConfigFlow
{
	internal const string ConnectionStepId = "connection";

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
	{
		return Task.FromResult(ConfigFlowResult.Step(ConnectionStep()));
	}

	public Task<ConfigFlowResult> SubmitAsync(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context,
		CancellationToken cancellationToken)
	{
		return Task.FromResult(stepId == ConnectionStepId
			? SubmitConnection(input)
			: ConfigFlowResult.Error(ConnectionStep(), Strings.ConfigFlow.UnknownStep()));
	}

	private ConfigFlowResult SubmitConnection(IReadOnlyDictionary<string, object?> input)
	{
		var host = Text(input, MuteDeckSettings.HostKey);
		var portText = Text(input, MuteDeckSettings.PortKey);
		var errors = new Dictionary<string, LocalizedText>(StringComparer.Ordinal);

		if (string.IsNullOrWhiteSpace(host))
		{
			errors[MuteDeckSettings.HostKey] = MacroDeckStrings.Validation.Required(Strings.ConfigFlow.Connection.Host.Label());
		}
		else if (!MuteDeckEndpoint.IsValidHost(host))
		{
			errors[MuteDeckSettings.HostKey] = Strings.ConfigFlow.Connection.InvalidHost();
		}

		if (!MuteDeckEndpoint.TryParsePort(portText, out var port))
		{
			errors[MuteDeckSettings.PortKey] = MacroDeckStrings.Validation.InvalidValue(Strings.ConfigFlow.Connection.Port.Label());
		}

		if (errors.Count > 0)
		{
			return ConfigFlowResult.Error(ConnectionStep(), errors.Values.First(), errors);
		}

		var endpoint = new MuteDeckEndpoint(host!.Trim(), port);
		var values = new Dictionary<string, ConfigFlowValue>(StringComparer.Ordinal)
		{
			[MuteDeckSettings.HostKey] = ConfigFlowValue.Plain(endpoint.Host),
			[MuteDeckSettings.PortKey] = ConfigFlowValue.Plain(endpoint.Port.ToString(CultureInfo.InvariantCulture))
		};

		// Saving rewrites the entry, so the credentials the running connection holds are carried over.
		// A token is only valid for the MuteDeck that issued it, so another endpoint starts unapproved.
		var credentials = tokenStore.Current;
		if (!string.IsNullOrEmpty(credentials.InstanceId))
		{
			values[MuteDeckSettings.InstanceIdKey] = ConfigFlowValue.Secret(credentials.InstanceId);
		}

		if (currentEndpoint is { } current && current != endpoint)
		{
			values[MuteDeckSettings.TokenKey] = ConfigFlowValue.Secret(string.Empty);
		}
		else if (!string.IsNullOrEmpty(credentials.Token))
		{
			values[MuteDeckSettings.TokenKey] = ConfigFlowValue.Secret(credentials.Token);
		}

		return ConfigFlowResult.Complete(MuteDeckSettings.EntryTitle, values);
	}

	private static string? Text(IReadOnlyDictionary<string, object?> input, string key)
	{
		return input.GetValueOrDefault(key) switch
		{
			null => null,
			string text => text,
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			var other => other.ToString()
		};
	}

	private static ConfigFlowStep ConnectionStep() => new()
	{
		StepId = ConnectionStepId,
		Title = Strings.ConfigFlow.Connection.Title(),
		Description = Strings.ConfigFlow.Connection.Description(),
		Fields =
		[
			ActionParameter.Text(MuteDeckSettings.HostKey,
				label: Strings.ConfigFlow.Connection.Host.Label(),
				description: Strings.ConfigFlow.Connection.Host.Description(),
				defaultValue: MuteDeckEndpoint.DefaultHost,
				required: true),
			ActionParameter.Number(MuteDeckSettings.PortKey,
				label: Strings.ConfigFlow.Connection.Port.Label(),
				description: Strings.ConfigFlow.Connection.Port.Description(),
				min: 1,
				max: 65535,
				step: 1,
				defaultValue: MuteDeckEndpoint.DefaultPort,
				required: true)
		]
	};
}
