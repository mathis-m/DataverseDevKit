using DataverseDevKit.Host.Bridge;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host;

public partial class MainPage : ContentPage
{
    private readonly JsonRpcBridge _bridge;
    private readonly ILogger<MainPage> _logger;

    public MainPage(JsonRpcBridge bridge, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _bridge = bridge;
        _logger = logger;

# if DEBUG
        hybridWebView.DefaultFile = "dev-redirect.html";
#else
        hybridWebView.DefaultFile = "index.html";
#endif

        // Wire up the bridge
        hybridWebView.RawMessageReceived += OnRawMessageReceived;
        
        _logger.LogInformation("HybridWebView initialized with DefaultFile: {DefaultFile}", hybridWebView.DefaultFile);
    }

    private async void OnRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.Message;
            _logger.LogInformation("📨 Received message from WebView: {Message}", message);
            
            if (string.IsNullOrEmpty(message))
            {
                _logger.LogWarning("Received empty message from WebView");
                return;
            }

            var response = await _bridge.HandleMessageAsync(message);
            
            _logger.LogInformation("📤 Sending response to WebView: {Response}", response);
            
            if (!string.IsNullOrEmpty(response))
            {
                // Send response back to JavaScript via EvaluateJavaScriptAsync
                var script = $"window.__ddkBridge?.handleResponse({response});";
                await hybridWebView.EvaluateJavaScriptAsync(script);
                _logger.LogInformation("✅ Response sent successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebView message");
        }
    }
}
