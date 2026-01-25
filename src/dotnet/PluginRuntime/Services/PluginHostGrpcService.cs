using Grpc.Core;
using Microsoft.Extensions.Logging;
using DataverseDevKit.PluginHost.Contracts;
using DataverseDevKit.PluginHost.Runtime;

namespace DataverseDevKit.PluginHost.Services;

/// <summary>
/// gRPC service implementation for plugin host communication.
/// </summary>
public class PluginHostGrpcService : PluginHostService.PluginHostServiceBase
{
    private readonly ILogger<PluginHostGrpcService> _logger;
    private readonly PluginLoader _pluginLoader;

    public PluginHostGrpcService(ILogger<PluginHostGrpcService> logger, PluginLoader pluginLoader)
    {
        _logger = logger;
        _pluginLoader = pluginLoader;
    }

    public override async Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Initialize request for plugin: {PluginId}", request.PluginId);

            var config = new Dictionary<string, string>(request.Config);
            
            // Create a logger for the plugin context
            var pluginLogger = _logger;
            await _pluginLoader.InitializePluginAsync(request.PluginId, request.StoragePath, config, pluginLogger, context.CancellationToken);

            var plugin = _pluginLoader.Plugin;

            return new InitializeResponse
            {
                Success = true,
                PluginName = plugin.Name,
                PluginVersion = plugin.Version
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin");
            return new InitializeResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<GetCommandsResponse> GetCommands(GetCommandsRequest request, ServerCallContext context)
    {
        try
        {
            var commands = await _pluginLoader.Plugin.GetCommandsAsync(context.CancellationToken);

            var response = new GetCommandsResponse();
            foreach (var cmd in commands)
            {
                response.Commands.Add(new Command
                {
                    Name = cmd.Name,
                    Label = cmd.Label,
                    Description = cmd.Description ?? string.Empty,
                    PayloadSchema = cmd.PayloadSchema ?? string.Empty
                });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get commands");
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<ExecuteResponse> Execute(ExecuteRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Execute request: {Command}", request.CommandName);

            var result = await _pluginLoader.Plugin.ExecuteAsync(request.CommandName, request.Payload, context.CancellationToken);

            // Serialize JsonElement to bytes - only serialization happens here
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            var resultBytes = Google.Protobuf.ByteString.CopyFromUtf8(resultJson);

            return new ExecuteResponse
            {
                Success = true,
                Result = resultBytes,
                CorrelationId = request.CorrelationId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command}", request.CommandName);
            return new ExecuteResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                CorrelationId = request.CorrelationId
            };
        }
    }

    public override async Task SubscribeEvents(SubscribeEventsRequest request, IServerStreamWriter<PluginEvent> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Events subscription started");

        try
        {
            var pluginContext = (PluginContextImpl)_pluginLoader.Context;
            var lastEventCount = 0;

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var events = pluginContext.Events;
                
                // Send new events
                for (int i = lastEventCount; i < events.Count; i++)
                {
                    var evt = events[i];
                    
                    // Filter by requested event types if specified
                    if (request.EventTypes.Count > 0 && !request.EventTypes.Contains(evt.Type))
                    {
                        continue;
                    }

                    var grpcEvent = new PluginEvent
                    {
                        Type = evt.Type,
                        Payload = evt.Payload,
                        Timestamp = evt.Timestamp.ToUnixTimeMilliseconds()
                    };

                    if (evt.Metadata != null)
                    {
                        foreach (var kvp in evt.Metadata)
                        {
                            grpcEvent.Metadata.Add(kvp.Key, kvp.Value);
                        }
                    }

                    await responseStream.WriteAsync(grpcEvent);
                }

                lastEventCount = events.Count;

                // Poll for new events every 100ms
                await Task.Delay(100, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in events subscription");
            throw;
        }
    }

    public override Task<ShutdownResponse> Shutdown(ShutdownRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Shutdown request received");

        // Trigger graceful shutdown
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            Environment.Exit(0);
        });

        return Task.FromResult(new ShutdownResponse { Success = true });
    }
}
