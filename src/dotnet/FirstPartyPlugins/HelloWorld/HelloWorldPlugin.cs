using System.Text.Json;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;

namespace Contoso.Ddk.HelloWorld;

/// <summary>
/// Hello World plugin demonstrating the DDK plugin architecture.
/// </summary>
public class HelloWorldPlugin : IToolPlugin
{
    private IPluginContext? _context;

    public string PluginId => "com.contoso.ddk.helloworld";
    public string Name => "Hello World";
    public string Version => "1.0.0";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _context.Logger.LogInformation("Hello World plugin initialized");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PluginCommand>> GetCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commands = new List<PluginCommand>
        {
            new()
            {
                Name = "echo",
                Label = "Echo Message",
                Description = "Returns the same message that was sent"
            },
            new()
            {
                Name = "getTime",
                Label = "Get Server Time",
                Description = "Returns the current server time in UTC"
            },
            new()
            {
                Name = "startHeartbeat",
                Label = "Start Heartbeat",
                Description = "Emits 5 heartbeat events (1 per second)"
            }
        };

        return Task.FromResult<IReadOnlyList<PluginCommand>>(commands);
    }

    public async Task<string> ExecuteAsync(string commandName, string payload, CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        return commandName switch
        {
            "echo" => await ExecuteEchoAsync(payload, cancellationToken),
            "getTime" => await ExecuteGetTimeAsync(cancellationToken),
            "startHeartbeat" => await ExecuteStartHeartbeatAsync(cancellationToken),
            _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
        };
    }

    private Task<string> ExecuteEchoAsync(string payload, CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("Echo command received: {Payload}", payload);
        
        // Parse the incoming message
        var request = JsonSerializer.Deserialize<EchoRequest>(payload);
        var response = new EchoResponse
        {
            Message = request?.Message ?? string.Empty,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(JsonSerializer.Serialize(response));
    }

    private Task<string> ExecuteGetTimeAsync(CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("GetTime command received");
        
        var response = new TimeResponse
        {
            ServerTime = DateTimeOffset.UtcNow
        };

        return Task.FromResult(JsonSerializer.Serialize(response));
    }

    private async Task<string> ExecuteStartHeartbeatAsync(CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("StartHeartbeat command received");

        // Start background task to emit 5 heartbeat events
        _ = Task.Run(async () =>
        {
            for (int i = 1; i <= 5; i++)
            {
                await Task.Delay(1000, cancellationToken);
                
                var heartbeat = new HeartbeatEvent
                {
                    Beat = i,
                    Total = 5,
                    Message = $"Heartbeat {i} of 5"
                };

                _context.EmitEvent(new PluginEvent
                {
                    Type = "heartbeat",
                    Payload = JsonSerializer.Serialize(heartbeat)
                });

                _context.Logger.LogInformation("Emitted heartbeat {Beat}/5", i);
            }
        }, cancellationToken);

        var response = new HeartbeatStartResponse
        {
            Started = true,
            Message = "Heartbeat started, will emit 5 events"
        };

        return JsonSerializer.Serialize(response);
    }

    public Task DisposeAsync()
    {
        _context?.Logger.LogInformation("Hello World plugin disposed");
        return Task.CompletedTask;
    }
}

// DTOs
internal record EchoRequest
{
    public string Message { get; init; } = string.Empty;
}

internal record EchoResponse
{
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; }
}

internal record TimeResponse
{
    public DateTimeOffset ServerTime { get; init; }
}

internal record HeartbeatEvent
{
    public int Beat { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = string.Empty;
}

internal record HeartbeatStartResponse
{
    public bool Started { get; init; }
    public string Message { get; init; } = string.Empty;
}
