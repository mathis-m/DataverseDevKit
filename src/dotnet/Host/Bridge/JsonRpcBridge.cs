using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Host.Services;

namespace DataverseDevKit.Host.Bridge;

/// <summary>
/// JSON-RPC bridge dispatcher for HybridWebView communication.
/// Provides a single ingress point for all frontend -> backend calls.
/// </summary>
public class JsonRpcBridge
{
    private readonly ILogger<JsonRpcBridge> _logger;
    private readonly ConnectionService _connectionService;
    private readonly AuthService _authService;
    private readonly PluginHostManager _pluginHostManager;
    private readonly StorageService _storageService;
    private readonly Dictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonRpcBridge(
        ILogger<JsonRpcBridge> logger,
        ConnectionService connectionService,
        AuthService authService,
        PluginHostManager pluginHostManager,
        StorageService storageService)
    {
        _logger = logger;
        _connectionService = connectionService;
        _authService = authService;
        _pluginHostManager = pluginHostManager;
        _storageService = storageService;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<string> HandleMessageAsync(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(message, _jsonOptions);
            
            if (request == null)
            {
                return CreateErrorResponse(null, -32700, "Parse error");
            }

            _logger.LogDebug("Handling JSON-RPC request: {Method} (id: {Id})", request.Method, request.Id);

            object? result = await DispatchMethodAsync(request.Method, request.Params);

            var response = new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };

            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling JSON-RPC message");
            return CreateErrorResponse(null, -32603, $"Internal error: {ex.Message}");
        }
    }

    private async Task<object?> DispatchMethodAsync(string method, JsonElement? paramsElement)
    {
        var parts = method.Split('.');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid method format: {method}");
        }

        var (namespace_, methodName) = (parts[0], parts[1]);

        return namespace_ switch
        {
            "connection" => await HandleConnectionMethodAsync(methodName, paramsElement),
            "auth" => await HandleAuthMethodAsync(methodName, paramsElement),
            "plugin" => await HandlePluginMethodAsync(methodName, paramsElement),
            "events" => await HandleEventsMethodAsync(methodName, paramsElement),
            "storage" => await HandleStorageMethodAsync(methodName, paramsElement),
            _ => throw new ArgumentException($"Unknown namespace: {namespace_}")
        };
    }

    private async Task<object?> HandleConnectionMethodAsync(string method, JsonElement? paramsElement)
    {
        return method switch
        {
            "list" => await _connectionService.ListConnectionsAsync(),
            "get" => await _connectionService.GetConnectionAsync(GetParam<string>(paramsElement, "id")),
            "setActive" => await _connectionService.SetActiveConnectionAsync(GetParam<string>(paramsElement, "id")),
            "add" => await _connectionService.AddConnectionAsync(DeserializeParams<AddConnectionParams>(paramsElement)),
            "remove" => await _connectionService.RemoveConnectionAsync(GetParam<string>(paramsElement, "id")),
            _ => throw new ArgumentException($"Unknown connection method: {method}")
        };
    }

    private async Task<object?> HandleAuthMethodAsync(string method, JsonElement? paramsElement)
    {
        return method switch
        {
            "login" => await _authService.LoginAsync(GetParam<string>(paramsElement, "connectionId")),
            "logout" => await _authService.LogoutAsync(),
            "getStatus" => await _authService.GetStatusAsync(),
            _ => throw new ArgumentException($"Unknown auth method: {method}")
        };
    }

    private async Task<object?> HandlePluginMethodAsync(string method, JsonElement? paramsElement)
    {
        return method switch
        {
            "list" => await _pluginHostManager.ListPluginsAsync(),
            "invoke" => await _pluginHostManager.InvokePluginCommandAsync(
                GetParam<string>(paramsElement, "pluginId"),
                GetParam<string>(paramsElement, "command"),
                GetParam<string>(paramsElement, "payload")),
            "getCommands" => await _pluginHostManager.GetPluginCommandsAsync(GetParam<string>(paramsElement, "pluginId")),
            _ => throw new ArgumentException($"Unknown plugin method: {method}")
        };
    }

    private Task<object?> HandleEventsMethodAsync(string method, JsonElement? paramsElement)
    {
        // Events are handled via streaming, not request/response
        return Task.FromResult<object?>(new { subscribed = true });
    }

    private async Task<object?> HandleStorageMethodAsync(string method, JsonElement? paramsElement)
    {
        return method switch
        {
            "get" => await _storageService.GetAsync(
                GetParam<string>(paramsElement, "pluginId"),
                GetParam<string>(paramsElement, "key")),
            "set" => await _storageService.SetAsync(
                GetParam<string>(paramsElement, "pluginId"),
                GetParam<string>(paramsElement, "key"),
                GetParam<string>(paramsElement, "value")),
            _ => throw new ArgumentException($"Unknown storage method: {method}")
        };
    }

    private T GetParam<T>(JsonElement? paramsElement, string paramName)
    {
        if (paramsElement == null || paramsElement.Value.ValueKind == JsonValueKind.Null)
        {
            throw new ArgumentException($"Missing parameter: {paramName}");
        }

        if (paramsElement.Value.TryGetProperty(paramName, out var prop))
        {
            return JsonSerializer.Deserialize<T>(prop.GetRawText(), _jsonOptions)!;
        }

        throw new ArgumentException($"Missing parameter: {paramName}");
    }

    private T DeserializeParams<T>(JsonElement? paramsElement)
    {
        if (paramsElement == null)
        {
            throw new ArgumentException("Missing parameters");
        }

        return JsonSerializer.Deserialize<T>(paramsElement.Value.GetRawText(), _jsonOptions)!;
    }

    private string CreateErrorResponse(object? id, int code, string message)
    {
        var response = new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };

        return JsonSerializer.Serialize(response, _jsonOptions);
    }
}

// JSON-RPC models
internal record JsonRpcRequest
{
    public object? Id { get; init; }
    public required string Method { get; init; }
    public JsonElement? Params { get; init; }
}

internal record JsonRpcResponse
{
    public object? Id { get; init; }
    public object? Result { get; init; }
}

internal record JsonRpcErrorResponse
{
    public object? Id { get; init; }
    public required JsonRpcError Error { get; init; }
}

internal record JsonRpcError
{
    public int Code { get; init; }
    public required string Message { get; init; }
}
