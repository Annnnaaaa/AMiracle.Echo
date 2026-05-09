using AMiracle.Echo.Abstractions.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AMiracle.Echo.Storage.EFCore;

public static class EfCoreServiceCollectionExtensions
{
    public static IServiceCollection AddEchoEfCoreStorage(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContext<EchoDbContext>(configure);
        services.AddScoped<IFeedbackStore, EfCoreFeedbackStore>();
        return services;
    }
}
