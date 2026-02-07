using DataverseDevKit.Host.Bridge;
using DataverseDevKit.Host.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataverseDevKit.Host;

public partial class MainPage : ContentPage, IDisposable
{
    private readonly JsonRpcBridge _bridge;
    private readonly PluginHostManager _pluginHostManager;
    private readonly ILogger<MainPage> _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public MainPage(JsonRpcBridge bridge, PluginHostManager pluginHostManager, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _bridge = bridge;
        _pluginHostManager = pluginHostManager;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

        // Wire up the bridge
        hybridWebView.RawMessageReceived += OnRawMessageReceived;

        // Subscribe to plugin events
        _pluginHostManager.PluginEventReceived += OnPluginEventReceived;

#if DEBUG
        if (IsDevServerAvailableAsync("http://localhost:5173"))
        {
            _logger.LogInformation("Dev server available, using dev-redirect.html");
            hybridWebView.DefaultFile = "dev-redirect.html";
        }
        else
        {
            _logger.LogInformation("Dev server not available, using index.html");
            hybridWebView.DefaultFile = "index.html";
        }
#else
        hybridWebView.DefaultFile = "index.html";
#endif

        _logger.LogInformation("HybridWebView initialized with DefaultFile: {DefaultFile}", hybridWebView.DefaultFile);
    }

    private bool IsDevServerAvailableAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = _httpClient.Send(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dev server check failed for {Url}", url);
            return false;
        }
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
                // Encode response as base64 to avoid escaping issues with nested JSON strings
                var base64Response = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(response));
                var script = $"window.__ddkBridge.handleResponse('{base64Response}');";
                _logger.LogDebug("Sending base64 encoded response");
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
                    // Encode event as base64 to avoid escaping issues
                    var base64Event = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(eventJson));
                    var script = $"window.__ddkBridge.handleResponse('{base64Event}');";
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

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources
            hybridWebView.RawMessageReceived -= OnRawMessageReceived;
            _pluginHostManager.PluginEventReceived -= OnPluginEventReceived;
            _httpClient?.Dispose();
        }

        // Dispose unmanaged resources here if any

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
