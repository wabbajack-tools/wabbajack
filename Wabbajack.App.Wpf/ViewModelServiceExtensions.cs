using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack;

public static class ViewModelServiceExtensions
{
    /// <summary>
    /// Registers all application ViewModels. Shared by App startup and the test ViewModel harness so
    /// both resolve ViewModels through identical registrations.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowVM>();
        services.AddTransient<NavigationVM>();
        services.AddTransient<HomeVM>();
        services.AddTransient<ModListGalleryVM>();
        services.AddTransient<CompilerHomeVM>();
        services.AddTransient<CompilerDetailsVM>();
        services.AddTransient<CompilerFileManagerVM>();
        services.AddTransient<CompilerMainVM>();
        services.AddTransient<InstallationVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<WebBrowserVM>();
        services.AddTransient<InfoVM>();
        services.AddTransient<ModListDetailsVM>();
        services.AddTransient<FileUploadVM>();
        services.AddTransient<AboutVM>();
        return services;
    }
}
