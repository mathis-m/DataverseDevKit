using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;

namespace Ddk.SamplePlugin;

/// <summary>
/// Sample plugin demonstrating the DDK plugin architecture.
/// This serves as a reference implementation for building plugins.
/// </summary>
public sealed class SamplePlugin : IToolPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IPluginContext? _context;

    public string PluginId => "com.ddk.sample";
    public string Name => "Sample Plugin";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _context.Logger.LogInformation("Sample plugin initialized");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PluginCommand>> GetCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commands = new List<PluginCommand>
        {
            new()
            {
                Name = "ping",
                Label = "Ping",
                Description = "Returns a pong response with timestamp"
            },
            new()
            {
                Name = "echo",
                Label = "Echo Message",
                Description = "Returns the same message that was sent"
            },
            new()
            {
                Name = "getInfo",
                Label = "Get Plugin Info",
                Description = "Returns information about this plugin"
            }
        };

        return Task.FromResult<IReadOnlyList<PluginCommand>>(commands);
    }

    public async Task<JsonElement> ExecuteAsync(string commandName, string payload, CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        _context.Logger.LogInformation("Executing command: {Command}", commandName);

        return commandName switch
        {
            "ping" => ExecutePing(),
            "echo" => ExecuteEcho(payload),
            "getInfo" => ExecuteGetInfo(),
            _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
        };
    }

    private JsonElement ExecutePing()
    {
        var response = new PingResponse
        {
            Message = "pong",
            Timestamp = DateTimeOffset.UtcNow,
            PluginId = PluginId
        };
        
        var json = JsonSerializer.Serialize(response, JsonOptions);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private JsonElement ExecuteEcho(string payload)
    {
        try
        {
            var request = JsonSerializer.Deserialize<EchoRequest>(payload);
            var response = new EchoResponse
            {
                Message = request?.Message ?? string.Empty,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            var json = JsonSerializer.Serialize(response, JsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            // If payload isn't valid JSON, just echo it back as-is
            return JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(new EchoResponse
                {
                    Message = payload,
                    ReceivedAt = DateTimeOffset.UtcNow
                }, JsonOptions));
        }
    }

    private JsonElement ExecuteGetInfo()
    {
        var response = new PluginInfoResponse
        {
            PluginId = PluginId,
            Name = Name,
            Version = Version,
            StoragePath = _context!.StoragePath,
            ServerTime = DateTimeOffset.UtcNow
        };
        
        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    public Task DisposeAsync()
    {
        _context?.Logger.LogInformation("Sample plugin disposed");
        return Task.CompletedTask;
    }
}

// Request/Response DTOs

internal sealed record EchoRequest
{
    public string Message { get; init; } = string.Empty;
}

internal sealed record EchoResponse
{
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; }
}

internal sealed record PingResponse
{
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string PluginId { get; init; } = string.Empty;
}

internal sealed record PluginInfoResponse
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string StoragePath { get; init; } = string.Empty;
    public DateTimeOffset ServerTime { get; init; }
}
