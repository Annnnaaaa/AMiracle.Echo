using AMiracle.Echo.Abstractions.Configuration;
using AMiracle.Echo.Abstractions.Email;
using AMiracle.Echo.Abstractions.Processing;
using AMiracle.Echo.Analysis.Abstractions;
using AMiracle.Echo.Server.Endpoints;
using AMiracle.Echo.Server.Services;
using AMiracle.Echo.Server.Services.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AMiracle.Echo.Server;

public static class EchoServiceCollectionExtensions
{
    public static IServiceCollection AddAmiracleEcho(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EchoOptions>(configuration);
        services.Configure<AnalysisOptions>(configuration.GetSection("Analysis"));
        return AddAmiracleEchoCore(services);
    }

    public static IServiceCollection AddAmiracleEcho(this IServiceCollection services, Action<EchoOptions> configure)
    {
        services.Configure(configure);
        return AddAmiracleEchoCore(services);
    }

    private static IServiceCollection AddAmiracleEchoCore(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FeedbackIngestionService>();
        services.AddSingleton<IdentityVerifier>();
        services.AddSingleton<ReceiptSigner>();
        services.AddHttpClient(ResendEmailSender.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
        // TryAdd: a host that registered its own IEchoEmailSender keeps it.
        services.TryAddSingleton<IEchoEmailSender>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<EchoOptions>>().Value.Email.Provider.Trim().ToLowerInvariant();
            return provider switch
            {
                "resend" => ActivatorUtilities.CreateInstance<ResendEmailSender>(sp),
                "smtp" => ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp),
                "" => new DisabledEmailSender(),
                _ => throw new InvalidOperationException($"Unknown AMiracle:Echo:Email:Provider '{provider}'. Use 'resend', 'smtp', or leave it empty."),
            };
        });
        services.AddSingleton<IFeedbackProcessor>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<EchoOptions>>().Value;
            return new MaxLengthProcessor(opts.MaxFeedbackTextChars);
        });
        services.AddSingleton<IFeedbackProcessor, NoOpRedactionProcessor>();
        services.AddHostedService<RetentionSweeper>();
        services.AddHostedService<AnalysisProcessor>();
        services.AddScoped<AdminTokenFilter>();
        return services;
    }

    public static IEndpointRouteBuilder MapAmiracleEcho(this IEndpointRouteBuilder routes)
    {
        routes.MapIngestion();
        routes.MapAdmin();
        routes.MapStaticAssets();
        return routes;
    }
}
