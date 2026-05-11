using AMiracle.Echo.Analysis.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AMiracle.Echo.Analysis.OpenAI;

public static class OpenAIServiceCollectionExtensions
{
    public static IServiceCollection AddEchoOpenAIAnalyzer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OpenAIAnalysisOptions>(configuration);
        services.AddHttpClient<IFeedbackAnalyzer, OpenAIFeedbackAnalyzer>();
        return services;
    }

    public static IServiceCollection AddEchoOpenAIAnalyzer(
        this IServiceCollection services,
        Action<OpenAIAnalysisOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IFeedbackAnalyzer, OpenAIFeedbackAnalyzer>();
        return services;
    }
}
