using Microsoft.Extensions.Logging;
using DataverseDevKit.Host.Bridge;
using DataverseDevKit.Host.Services;

namespace DataverseDevKit.Host;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
#if WINDOWS
            .ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<Microsoft.Maui.Controls.HybridWebView, Platforms.Windows.CustomHybridWebViewHandler>();
            })
#endif
            ;

        // Register App for DI (it has constructor dependencies)
        builder.Services.AddSingleton<App>();

        // Pages
        builder.Services.AddTransient<MainPage>();
        
        // Services
        builder.Services.AddSingleton<TokenCacheService>();
        builder.Services.AddSingleton<ConnectionService>();
        builder.Services.AddSingleton<TokenProviderService>();
        builder.Services.AddSingleton<TokenCallbackServer>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<StorageService>();
        builder.Services.AddSingleton<PluginHostManager>();
        builder.Services.AddSingleton<JsonRpcBridge>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddConsole();
        builder.Services.AddHybridWebViewDeveloperTools();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
#endif

        return builder.Build();
    }
}
