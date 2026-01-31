using DataverseDevKit.Host.Services;

namespace DataverseDevKit.Host;

public partial class App : Application
{
    private readonly PluginHostManager _pluginHostManager;

    public App(PluginHostManager pluginHostManager)
    {
        InitializeComponent();
        _pluginHostManager = pluginHostManager;

        // Initialize child process tracking so plugin workers are killed when the host exits
        ChildProcessTracker.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // Handle window closing to stop all plugin workers
        window.Destroying += OnWindowDestroying;

#if WINDOWS
        // Start maximized on Windows
        window.Created += (s, e) =>
        {
            var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow is not null)
            {
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        };
#endif

        return window;
    }

    private async void OnWindowDestroying(object? sender, EventArgs e)
    {
        try
        {
            // Stop all plugin workers gracefully before the app exits
            await _pluginHostManager.StopAllPluginInstancesAsync();
        }
        catch
        {
            // Ensure cleanup happens even if async stop fails
            _pluginHostManager.Dispose();
        }
    }
}
