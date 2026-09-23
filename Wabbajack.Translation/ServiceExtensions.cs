using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Translation.Nexus;
using Wabbajack.Translation.Steam;

namespace Wabbajack.Translation;

public static class ServiceExtensions
{
    public static IServiceCollection AddTranslation(this IServiceCollection services)
    {
        services.AddSingleton<NexusTranslationFinder>();
        services.AddSingleton<TranslationDownloader>();
        services.AddSingleton<LanguageSupportBuilder>();
        services.AddSingleton<VoiceDownloader>();
        services.AddSingleton<LanguagePackInstaller>();
        services.AddSingleton<TranslationRunner>();
        return services;
    }
}
