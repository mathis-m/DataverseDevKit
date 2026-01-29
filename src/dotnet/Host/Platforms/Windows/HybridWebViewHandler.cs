using Microsoft.Maui.Platform;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Platforms.Windows;

/// <summary>
/// Custom HybridWebView handler for Windows to allow localhost dev server access
/// </summary>
public class CustomHybridWebViewHandler : Microsoft.Maui.Handlers.HybridWebViewHandler
{
    private static readonly HttpClient _httpClient = new();

    protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
    {
        base.ConnectHandler(platformView);

        // Configure WebView2 to allow localhost access for dev servers
        platformView.CoreWebView2Initialized += async (sender, args) =>
        {
            var core = sender.CoreWebView2;
            if (core == null)
            {
                return;
            }

            // 🔥 THIS is the important line
            await core.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.AllProfile
            );

            // Optional but recommended
            var settings = core.Settings;
            settings.AreDevToolsEnabled = true;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
        };
    }
}
