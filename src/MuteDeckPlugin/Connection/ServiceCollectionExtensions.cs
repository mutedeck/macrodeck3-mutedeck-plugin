using Microsoft.Extensions.DependencyInjection;

namespace MuteDeckPlugin.Connection;

internal static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMuteDeck(this IServiceCollection services)
	{
		services.AddSingleton<IMuteDeckConnection, MuteDeckConnection>();
		services.AddSingleton<HostTokenStore>();
		return services;
	}
}
