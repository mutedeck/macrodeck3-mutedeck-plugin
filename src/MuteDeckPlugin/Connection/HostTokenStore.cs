using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.ConfigFlow;
using MuteDeck.PluginClient;
using MuteDeckPlugin.Setup;
using Serilog;

namespace MuteDeckPlugin.Connection;

/// <summary>
/// Keeps MuteDeck's approval token and this install's instance id as secrets of the integration's
/// config entry, so they land in the host's encrypted secret store. A copy in memory covers the window
/// where the host cannot be reached: losing a freshly issued token there would mean approving again.
/// </summary>
internal sealed class HostTokenStore : ITokenStore
{
	private readonly ILogger _logger;
	private readonly Lock _gate = new();

	private Binding? _binding;
	private StoredCredentials _memory = new();

	public HostTokenStore(ILogger logger)
	{
		_logger = logger.ForContext<HostTokenStore>();
	}

	/// <summary>The credentials last loaded or saved, for a config flow that has to carry them over.</summary>
	public StoredCredentials Current
	{
		get
		{
			lock (_gate)
			{
				return Copy(_memory);
			}
		}
	}

	public void Bind(IIntegrationConfig config, Guid entryId)
	{
		lock (_gate)
		{
			if (_binding is { } existing && existing.EntryId != entryId)
			{
				// Another entry belongs to another setup; its credentials load from the host.
				_memory = new StoredCredentials();
			}

			_binding = new Binding(config, entryId);
		}
	}

	/// <summary>Drops the memory copy, so credentials of a previous endpoint can never stand in for the new one.</summary>
	public void ForgetMemory()
	{
		lock (_gate)
		{
			_memory = new StoredCredentials();
		}
	}

	public void Unbind()
	{
		lock (_gate)
		{
			_binding = null;
		}
	}

	public async Task<StoredCredentials> LoadAsync(CancellationToken cancellationToken = default)
	{
		var binding = CurrentBinding();
		if (binding is null)
		{
			return Current;
		}

		try
		{
			var token = await binding.Config.GetSecretAsync(binding.EntryId, MuteDeckSettings.TokenKey, cancellationToken)
				.ConfigureAwait(false);
			var instanceId = await binding.Config.GetSecretAsync(binding.EntryId, MuteDeckSettings.InstanceIdKey, cancellationToken)
				.ConfigureAwait(false);

			// A successful read is authoritative: an empty secret means "no token", for example after the
			// setup moved to another MuteDeck. The memory copy only stands in when the host cannot answer.
			lock (_gate)
			{
				_memory = new StoredCredentials { Token = NullIfEmpty(token), InstanceId = NullIfEmpty(instanceId) };
				return Copy(_memory);
			}
		}
		catch (Exception exception) when (IsHostFailure(exception, cancellationToken))
		{
			_logger.Warning("Could not read the MuteDeck credentials from Macro Deck, using the copy in memory: {Message}",
				exception.Message);
			return Current;
		}
	}

	public async Task SaveAsync(StoredCredentials credentials, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(credentials);
		Binding? binding;
		lock (_gate)
		{
			_memory = Copy(credentials);
			binding = _binding;
		}

		if (binding is not null)
		{
			await WriteAsync(binding, credentials.Token, credentials.InstanceId, cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task ClearAsync(CancellationToken cancellationToken = default)
	{
		Binding? binding;
		lock (_gate)
		{
			_memory = new StoredCredentials();
			binding = _binding;
		}

		if (binding is not null)
		{
			await WriteAsync(binding, null, null, cancellationToken).ConfigureAwait(false);
		}
	}

	// IIntegrationConfig has no way to delete a secret, so an empty value stands for "none".
	private async Task WriteAsync(Binding binding, string? token, string? instanceId, CancellationToken cancellationToken)
	{
		try
		{
			await binding.Config.SetSecretAsync(binding.EntryId, MuteDeckSettings.TokenKey, token ?? string.Empty, cancellationToken)
				.ConfigureAwait(false);
			await binding.Config.SetSecretAsync(binding.EntryId, MuteDeckSettings.InstanceIdKey, instanceId ?? string.Empty, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exception) when (IsHostFailure(exception, cancellationToken))
		{
			_logger.Warning("Could not store the MuteDeck credentials in Macro Deck; keeping them in memory until the next save: {Message}",
				exception.Message);
		}
	}

	private Binding? CurrentBinding()
	{
		lock (_gate)
		{
			return _binding;
		}
	}

	private static bool IsHostFailure(Exception exception, CancellationToken cancellationToken)
	{
		return exception is HostInvocationException or InvalidOperationException
			|| (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
	}

	private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

	private static StoredCredentials Copy(StoredCredentials source)
	{
		return new StoredCredentials { Token = source.Token, InstanceId = source.InstanceId };
	}

	private sealed record Binding(IIntegrationConfig Config, Guid EntryId);
}
