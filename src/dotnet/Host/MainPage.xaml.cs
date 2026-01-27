using DataverseDevKit.Host.Bridge;
using DataverseDevKit.Host.Services;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DataverseDevKit.Host;

public partial class MainPage : ContentPage
{
    private readonly JsonRpcBridge _bridge;
    private readonly PluginHostManager _pluginHostManager;
    private readonly ILogger<MainPage> _logger;

    public MainPage(JsonRpcBridge bridge, PluginHostManager pluginHostManager, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _bridge = bridge;
        _pluginHostManager = pluginHostManager;
        _logger = logger;

# if DEBUG
        hybridWebView.DefaultFile = "dev-redirect.html";
#else
        hybridWebView.DefaultFile = "index.html";
#endif

        // Wire up the bridge
        hybridWebView.RawMessageReceived += OnRawMessageReceived;
        
        // Subscribe to plugin events
        _pluginHostManager.PluginEventReceived += OnPluginEventReceived;
        
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
                // Use JavaScriptEncoder to properly escape the JSON string for JavaScript
                var escapedResponse = JavaScriptEncoder.Default.Encode(response);
                var script = $"window.__ddkBridge.handleResponse('{escapedResponse}');";
                await hybridWebView.EvaluateJavaScriptAsync(script);
                _logger.LogInformation("✅ Response sent successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebView message");
        }
    }

    private async void OnPluginEventReceived(object? sender, PluginEventArgs e)
    {
        try
        {
            var evt = e.Event;
            _logger.LogInformation("🎉 Plugin event received: {PluginId} - {EventType}", evt.PluginId, evt.Type);

            // Parse payload to avoid double-stringification
            JsonElement? payloadElement = null;
            if (!string.IsNullOrEmpty(evt.Payload))
            {
                try
                {
                    payloadElement = JsonSerializer.Deserialize<JsonElement>(evt.Payload);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to parse event payload as JSON, using raw string");
                }
            }

            // Convert gRPC PluginEvent to JSON for frontend
            var eventJson = JsonSerializer.Serialize(new
            {
                pluginId = evt.PluginId,
                type = evt.Type,
                payload = payloadElement,
                timestamp = evt.Timestamp,
                metadata = evt.Metadata
            }, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            // Marshal to UI thread before interacting with WebView (COM component)
            await Dispatcher.DispatchAsync(async () =>
            {
                try
                {
                    // Forward to frontend
                    var escapedEvent = JavaScriptEncoder.Default.Encode(eventJson);
                    var script = $"window.__ddkBridge.handleResponse('{escapedEvent}');";
                    await hybridWebView.EvaluateJavaScriptAsync(script);
                    
                    _logger.LogInformation("✅ Event forwarded to frontend: {EventType}", evt.Type);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating JavaScript in WebView");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding plugin event to frontend");
        }
    }
}
