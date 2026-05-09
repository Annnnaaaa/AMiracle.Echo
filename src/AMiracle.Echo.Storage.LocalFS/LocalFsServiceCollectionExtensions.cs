using AMiracle.Echo.Abstractions.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace AMiracle.Echo.Storage.LocalFS;

public static class LocalFsServiceCollectionExtensions
{
    public static IServiceCollection AddEchoLocalFileBlobStore(
        this IServiceCollection services,
        Action<LocalFileBlobStoreOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IBlobStore, LocalFileBlobStore>();
        return services;
    }
}
