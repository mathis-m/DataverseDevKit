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

        // Pages
        builder.Services.AddTransient<MainPage>();
        
        // Services
        builder.Services.AddSingleton<JsonRpcBridge>();
        builder.Services.AddSingleton<PluginHostManager>();
        builder.Services.AddSingleton<ConnectionService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<DataverseService>();
        builder.Services.AddSingleton<StorageService>();

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
