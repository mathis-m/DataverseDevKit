using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using DataverseDevKit.PluginHost.Contracts;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// EventArgs wrapper for plugin events.
/// </summary>
public class PluginEventArgs : EventArgs
{
    public required PluginEvent Event { get; init; }
}

/// <summary>
/// Manages out-of-process plugin worker lifecycle and communication.
/// Each plugin instance runs in isolation with its own worker process.
/// </summary>
public sealed class PluginHostManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<PluginHostManager> _logger;
    private readonly StorageService _storageService;
    private readonly TokenCallbackServer _tokenCallbackServer;
    private readonly ConnectionService _connectionService;
    private readonly Dictionary<string, PluginWorkerInfo> _workers = new();
    private readonly Dictionary<string, TaskCompletionSource<PluginWorkerInfo>> _pendingStarts = new();
    private readonly string _pluginsBasePath;
    private readonly HttpClient _httpClient;
    private readonly object _lock = new();
    
    // Base URL for the MAUI
    private const string BaseUrl = "https://0.0.0.1/";

    // Event forwarding callback
    public event EventHandler<PluginEventArgs>? PluginEventReceived;

    public PluginHostManager(
        ILogger<PluginHostManager> logger, 
        StorageService storageService,
        TokenCallbackServer tokenCallbackServer,
        ConnectionService connectionService)
    {
        _logger = logger;
        _storageService = storageService;
        _tokenCallbackServer = tokenCallbackServer;
        _connectionService = connectionService;

        // Plugins are located in wwwroot/plugins after build
        var appPath = AppContext.BaseDirectory;
        _pluginsBasePath = Path.Combine(appPath, "wwwroot", "plugins");
        
        // Fallback to src/plugins for development
        if (!Directory.Exists(_pluginsBasePath))
        {
            _pluginsBasePath = Path.Combine(appPath, "..", "..", "..", "..", "..", "plugins");
            _pluginsBasePath = Path.GetFullPath(_pluginsBasePath);
        }

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        _logger.LogInformation("Plugins base path: {Path}", _pluginsBasePath);
    }

    /// <summary>
    /// Lists all available plugins from the plugins directory.
    /// </summary>
    public async Task<List<PluginInfo>> ListPluginsAsync()
    {
        var plugins = new List<PluginInfo>();

        if (!Directory.Exists(_pluginsBasePath))
        {
            _logger.LogWarning("Plugins directory not found: {Path}", _pluginsBasePath);
            return plugins;
        }

        foreach (var pluginDir in Directory.GetDirectories(_pluginsBasePath))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.manifest.json");

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = await LoadManifestAsync(manifestPath);
                if (manifest == null) continue;

                var uiEntry = await ResolveUiEntryUrlAsync(manifest, pluginDir);

                plugins.Add(new PluginInfo
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Version = manifest.Version,
                    Description = manifest.Description,
                    Author = manifest.Author ?? "Unknown",
                    Category = manifest.Category ?? "other",
                    Company = manifest.Company,
                    Icon = manifest.Icon,
                    Commands = new List<PluginCommand>(),
                    UiEntry = uiEntry,
                    UiModule = manifest.Ui?.Module,
                    UiScope = manifest.Ui?.Scope,
                    IsRunning = HasRunningInstance(manifest.Id)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading plugin manifest: {Path}", manifestPath);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Resolves the UI entry URL from the manifest.
    /// Uses devEntry in development mode if available, otherwise uses entry.
    /// </summary>
    private async Task<string> ResolveUiEntryUrlAsync(PluginManifest manifest, string pluginDir)
    {
        if (manifest.Ui == null)
        {
            return string.Empty;
        }

        // In development mode, prefer devEntry if it's available (server is running)
        if (IsDevelopmentMode() && !string.IsNullOrEmpty(manifest.Ui.DevEntry))
        {
            if (await IsDevServerAvailableAsync(manifest.Ui.DevEntry))
            {
                _logger.LogInformation("Using dev server for plugin UI: {DevEntry}", manifest.Ui.DevEntry);
                return manifest.Ui.DevEntry;
            }
            _logger.LogDebug("Dev server not available at {DevEntry}, falling back to production entry", manifest.Ui.DevEntry);
        }

        // Production mode: resolve relative path against base URL
        if (!string.IsNullOrEmpty(manifest.Ui.Entry))
        {
            var pluginName = Path.GetFileName(pluginDir);
            var relativePath = $"plugins/{pluginName}/{manifest.Ui.Entry.TrimStart('.', '/')}";
            var fullUrl = new Uri(new Uri(BaseUrl), relativePath).ToString();
            _logger.LogDebug("Resolved plugin UI entry: {FullUrl}", fullUrl);
            return fullUrl;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if a dev server is available by sending a HEAD request.
    /// </summary>
    private async Task<bool> IsDevServerAvailableAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dev server check failed for {Url}", url);
            return false;
        }
    }

    private bool IsDevelopmentMode()
    {
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        return isDev || Debugger.IsAttached;
    }

    /// <summary>
    /// Starts a new plugin instance with isolation.
    /// </summary>
    /// <param name="pluginId">The plugin ID to start.</param>
    /// <param name="instanceId">Unique instance ID for isolation (e.g., tab ID).</param>
    /// <returns>Information about the started plugin instance.</returns>
    public async Task<PluginInstanceInfo> StartPluginInstanceAsync(string pluginId, string instanceId)
    {
        var workerKey = GetWorkerKey(pluginId, instanceId);
        Task<PluginWorkerInfo>? waitTask = null;
        TaskCompletionSource<PluginWorkerInfo>? tcs = null;

        lock (_lock)
        {
            // Check if worker already exists
            if (_workers.TryGetValue(workerKey, out _))
            {
                return new PluginInstanceInfo
                {
                    PluginId = pluginId,
                    InstanceId = instanceId,
                    IsNew = false
                };
            }

            // Check if another caller is already starting this worker
            if (_pendingStarts.TryGetValue(workerKey, out var pendingTcs))
            {
                _logger.LogDebug("Another caller is starting plugin {PluginId} (instance: {InstanceId}), waiting...", 
                    pluginId, instanceId);
                waitTask = pendingTcs.Task;
            }
            else
            {
                // We're the first - create pending entry
                tcs = new TaskCompletionSource<PluginWorkerInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingStarts[workerKey] = tcs;
            }
        }

        // If another caller is starting, wait for them
        if (waitTask != null)
        {
            await waitTask;
            return new PluginInstanceInfo
            {
                PluginId = pluginId,
                InstanceId = instanceId,
                IsNew = false
            };
        }

        // We're responsible for starting the worker
        _logger.LogInformation("Starting plugin instance: {PluginId} (instance: {InstanceId})", pluginId, instanceId);

        try
        {
            var (manifest, pluginDir) = await FindPluginAsync(pluginId);

            // Generate socket path - Host controls this
            var socketPath = GenerateSocketPath(pluginId, instanceId);

            // Ensure socket directory exists and clean up old socket
            var socketDir = Path.GetDirectoryName(socketPath);
            if (!string.IsNullOrEmpty(socketDir) && !Directory.Exists(socketDir))
            {
                Directory.CreateDirectory(socketDir);
            }
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }

            // Locate executables and assemblies
            var pluginHostExe = FindPluginHostExecutable();
            var assemblyPath = ResolveAssemblyPath(manifest, pluginDir);
            var storagePath = _storageService.GetPluginStoragePath($"{pluginId}/{instanceId}");

            // Start worker process with all configuration passed as arguments
            var worker = await StartWorkerProcessAsync(
                pluginHostExe,
                assemblyPath,
                socketPath,
                pluginId,
                instanceId,
                storagePath);

            lock (_lock)
            {
                _workers[workerKey] = worker;
                _pendingStarts.Remove(workerKey);
            }

            tcs!.SetResult(worker);

            return new PluginInstanceInfo
            {
                PluginId = pluginId,
                InstanceId = instanceId,
                IsNew = true
            };
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _pendingStarts.Remove(workerKey);
            }
            tcs!.SetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Stops a plugin instance.
    /// </summary>
    public async Task StopPluginInstanceAsync(string pluginId, string instanceId)
    {
        var workerKey = GetWorkerKey(pluginId, instanceId);

        PluginWorkerInfo? worker;
        lock (_lock)
        {
            if (!_workers.TryGetValue(workerKey, out worker))
            {
                return;
            }
            _workers.Remove(workerKey);
        }

        _logger.LogInformation("Stopping plugin instance: {PluginId} (instance: {InstanceId})", pluginId, instanceId);
        await TerminateWorkerAsync(worker);
    }

    /// <summary>
    /// Stops all running plugin instances.
    /// </summary>
    public async Task StopAllPluginInstancesAsync()
    {
        List<PluginWorkerInfo> workers;
        lock (_lock)
        {
            workers = _workers.Values.ToList();
            _workers.Clear();
        }

        _logger.LogInformation("Stopping all plugin instances ({Count} workers)", workers.Count);

        // Stop all workers in parallel
        var stopTasks = workers.Select(TerminateWorkerAsync);
        await Task.WhenAll(stopTasks);

        _logger.LogInformation("All plugin instances stopped");
    }

    /// <summary>
    /// Terminates a worker process gracefully, then forcefully if needed.
    /// Waits for file handles to be released.
    /// </summary>
    private async Task TerminateWorkerAsync(PluginWorkerInfo worker)
    {
        try
        {
            // Dispose gRPC channel first to release network resources
            worker.Channel?.Dispose();

            // Request graceful shutdown via gRPC
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await worker.Client.ShutdownAsync(new ShutdownRequest(), cancellationToken: cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Graceful shutdown request failed for {PluginId}, will force kill", worker.PluginId);
            }

            // Wait for process to exit gracefully
            if (!worker.Process.HasExited)
            {
                var exited = await WaitForExitAsync(worker.Process, TimeSpan.FromSeconds(5));
                if (!exited)
                {
                    _logger.LogWarning("Plugin worker {PluginId} did not exit gracefully, forcing kill", worker.PluginId);
                    worker.Process.Kill(entireProcessTree: true);
                    await WaitForExitAsync(worker.Process, TimeSpan.FromSeconds(3));
                }
            }

            _logger.LogDebug("Plugin worker {PluginId} terminated (exit code: {ExitCode})", 
                worker.PluginId, worker.Process.HasExited ? worker.Process.ExitCode : -1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error terminating worker for plugin {PluginId}", worker.PluginId);

            // Last resort: kill the process
            try
            {
                if (!worker.Process.HasExited)
                {
                    worker.Process.Kill(entireProcessTree: true);
                }
            }
            catch { /* ignore */ }
        }
        finally
        {
            CleanupWorker(worker);

            // Give OS time to release file handles
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Waits for a process to exit with a timeout.
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets commands available from a plugin instance.
    /// </summary>
    public async Task<List<PluginCommand>> GetPluginCommandsAsync(string pluginId, string instanceId)
    {
        var worker = await EnsurePluginWorkerAsync(pluginId, instanceId);

        var response = await worker.Client.GetCommandsAsync(new GetCommandsRequest());

        return response.Commands.Select(c => new PluginCommand
        {
            Name = c.Name,
            Label = c.Label,
            Description = c.Description
        }).ToList();
    }

    /// <summary>
    /// Invokes a command on a plugin instance.
    /// </summary>
    public async Task<JsonElement> InvokePluginCommandAsync(string pluginId, string instanceId, string command, string payload)
    {
        var worker = await EnsurePluginWorkerAsync(pluginId, instanceId);

        var correlationId = Guid.NewGuid().ToString();
        var request = new ExecuteRequest
        {
            CommandName = command,
            Payload = payload,
            CorrelationId = correlationId
        };

        _logger.LogInformation("Invoking plugin command: {PluginId}.{Command} (instance: {InstanceId})", 
            pluginId, command, instanceId);

        var response = await worker.Client.ExecuteAsync(request);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Plugin command failed: {response.ErrorMessage}");
        }

        // Deserialize bytes to JsonElement to avoid double JSON encoding
        var resultJson = response.Result.ToStringUtf8();
        return JsonSerializer.Deserialize<JsonElement>(resultJson, JsonOptions);
    }

    // Legacy methods for backward compatibility (default instance)
    public Task<List<PluginCommand>> GetPluginCommandsAsync(string pluginId) 
        => GetPluginCommandsAsync(pluginId, "default");

    public Task<JsonElement> InvokePluginCommandAsync(string pluginId, string command, string payload) 
        => InvokePluginCommandAsync(pluginId, "default", command, payload);

    private string GetWorkerKey(string pluginId, string instanceId) => $"{pluginId}::{instanceId}";

    private bool HasRunningInstance(string pluginId)
    {
        lock (_lock)
        {
            return _workers.Keys.Any(k => k.StartsWith($"{pluginId}::"));
        }
    }

    private async Task<PluginWorkerInfo> EnsurePluginWorkerAsync(string pluginId, string instanceId)
    {
        var workerKey = GetWorkerKey(pluginId, instanceId);
        Task<PluginWorkerInfo>? waitTask = null;

        lock (_lock)
        {
            if (_workers.TryGetValue(workerKey, out var existing))
            {
                return existing;
            }

            // Check if a start is pending - we can wait on it directly
            if (_pendingStarts.TryGetValue(workerKey, out var pendingTcs))
            {
                waitTask = pendingTcs.Task;
            }
        }

        if (waitTask != null)
        {
            return await waitTask;
        }

        // No worker and no pending start - start one
        await StartPluginInstanceAsync(pluginId, instanceId);

        lock (_lock)
        {
            return _workers[workerKey];
        }
    }

    private string GenerateSocketPath(string pluginId, string instanceId)
    {
        var sanitizedPluginId = pluginId.Replace(".", "-", StringComparison.Ordinal);
        var sanitizedInstanceId = instanceId.Replace(".", "-", StringComparison.Ordinal).Replace("/", "-", StringComparison.Ordinal);
        var pid = Environment.ProcessId;

        return OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), $"ddk-{pid}-{sanitizedPluginId}-{sanitizedInstanceId}.sock")
            : $"/tmp/ddk/{pid}/{sanitizedPluginId}-{sanitizedInstanceId}.sock";
    }

    private async Task<(PluginManifest manifest, string pluginDir)> FindPluginAsync(string pluginId)
    {
        if (!Directory.Exists(_pluginsBasePath))
        {
            throw new DirectoryNotFoundException($"Plugins directory not found: {_pluginsBasePath}");
        }

        foreach (var dir in Directory.GetDirectories(_pluginsBasePath))
        {
            var manifestPath = Path.Combine(dir, "plugin.manifest.json");
            if (!File.Exists(manifestPath)) continue;

            var manifest = await LoadManifestAsync(manifestPath);
            if (manifest?.Id == pluginId)
            {
                return (manifest, dir);
            }
        }

        throw new FileNotFoundException($"Plugin not found: {pluginId}");
    }

    private async Task<PluginManifest?> LoadManifestAsync(string manifestPath)
    {
        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private string FindPluginHostExecutable()
    {
        // Check alongside the host application
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "DataverseDevKit.PluginHost.exe"),
            Path.Combine(AppContext.BaseDirectory, "DataverseDevKit.PluginHost"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Development fallback: search in PluginRuntime build output
        var devPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "PluginRuntime", "bin"));

        if (Directory.Exists(devPath))
        {
            var found = Directory.GetFiles(devPath, "DataverseDevKit.PluginHost.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (found != null) return found;

            found = Directory.GetFiles(devPath, "DataverseDevKit.PluginHost", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (found != null) return found;
        }

        throw new FileNotFoundException("Plugin host executable not found");
    }

    private string ResolveAssemblyPath(PluginManifest manifest, string pluginDir)
    {
        var assemblyName = manifest.Backend?.Assembly;
        if (string.IsNullOrEmpty(assemblyName))
        {
            throw new InvalidOperationException($"Plugin manifest does not specify an assembly: {manifest.Id}");
        }

        // Resolve relative to plugin directory
        var assemblyPath = Path.GetFullPath(Path.Combine(pluginDir, assemblyName));

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");
        }

        _logger.LogInformation("Resolved plugin assembly: {AssemblyPath}", assemblyPath);
        return assemblyPath;
    }

    private async Task<PluginWorkerInfo> StartWorkerProcessAsync(
        string pluginHostExe,
        string assemblyPath,
        string socketPath,
        string pluginId,
        string instanceId,
        string storagePath)
    {
        // Pass all configuration via command line arguments for clarity and control
        var psi = new ProcessStartInfo
        {
            FileName = pluginHostExe,
            Arguments = $"--socket \"{socketPath}\" --assembly \"{assemblyPath}\" --plugin-id \"{pluginId}\" --instance-id \"{instanceId}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _logger.LogInformation("Starting plugin worker: {Exe} {Args}", pluginHostExe, psi.Arguments);

        var process = Process.Start(psi) 
            ?? throw new InvalidOperationException("Failed to start plugin worker process");

        // Track this process so it's automatically terminated when the host exits
        ChildProcessTracker.AddProcess(process);

        // Capture stderr in background
        var errorOutput = new System.Text.StringBuilder();
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
            {
                errorOutput.AppendLine(line);
                _logger.LogWarning("Plugin worker stderr: {Line}", line);
            }
        });

        // Wait for READY signal
        var readyTask = WaitForReadySignalAsync(process.StandardOutput);
        var timeoutTask = Task.Delay(10000);

        if (await Task.WhenAny(readyTask, timeoutTask) == timeoutTask)
        {
            process.Kill();
            throw new TimeoutException($"Plugin worker failed to start. Stderr: {errorOutput}");
        }

        var ready = await readyTask;
        if (!ready)
        {
            process.Kill();
            throw new InvalidOperationException($"Plugin worker signaled failure. Stderr: {errorOutput}");
        }

        _logger.LogInformation("Plugin worker ready, connecting to socket: {SocketPath}", socketPath);

        // Allow socket to be created
        await Task.Delay(200);

        // Connect via gRPC
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = CreateUnixSocketHandler(socketPath)
        });

        var client = new PluginHostService.PluginHostServiceClient(channel);

        // Get the token callback socket path (starts server if needed)
        var tokenCallbackSocket = await _tokenCallbackServer.StartAsync();
        
        // Get active connection info
        var activeConnection = await _connectionService.GetActiveConnectionAsync();

        // Initialize the plugin with token callback info
        var initRequest = new InitializeRequest
        {
            PluginId = pluginId,
            StoragePath = storagePath,
            TokenCallbackSocket = tokenCallbackSocket,
            ActiveConnectionId = activeConnection?.Id ?? string.Empty,
            ActiveConnectionUrl = activeConnection?.Url ?? string.Empty
        };

        var initResponse = await client.InitializeAsync(initRequest);

        if (!initResponse.Success)
        {
            process.Kill();
            throw new InvalidOperationException($"Plugin initialization failed: {initResponse.ErrorMessage}");
        }

        _logger.LogInformation("Plugin instance started: {PluginName} v{Version} (instance: {InstanceId})",
            initResponse.PluginName, initResponse.PluginVersion, instanceId);

        var worker = new PluginWorkerInfo
        {
            PluginId = pluginId,
            InstanceId = instanceId,
            Process = process,
            Client = client,
            Channel = channel,
            SocketPath = socketPath
        };

        // Start event subscription in background
        _ = Task.Run(() => SubscribeToPluginEventsAsync(worker));

        return worker;
    }

    private static async Task<bool> WaitForReadySignalAsync(StreamReader stdout)
    {
        var line = await stdout.ReadLineAsync();
        return line == "READY";
    }

    private static SocketsHttpHandler CreateUnixSocketHandler(string socketPath)
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);

                var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
                await socket.ConnectAsync(endpoint, cancellationToken);

                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
        };
    }

    /// <summary>
    /// Subscribes to plugin events and forwards them to the frontend.
    /// </summary>
    private async Task SubscribeToPluginEventsAsync(PluginWorkerInfo worker)
    {
        var cts = new CancellationTokenSource();
        worker.EventSubscriptionCts = cts;

        try
        {
            var request = new SubscribeEventsRequest();
            // Subscribe to all event types (empty list means all)
            
            using var streamingCall = worker.Client.SubscribeEvents(request, cancellationToken: cts.Token);

            _logger.LogInformation("Subscribed to events for plugin {PluginId} (instance: {InstanceId})",
                worker.PluginId, worker.InstanceId);

            while (await streamingCall.ResponseStream.MoveNext(cts.Token))
            {
                var evt = streamingCall.ResponseStream.Current;
                
                _logger.LogDebug("Received event from plugin {PluginId}: {EventType}", 
                    worker.PluginId, evt.Type);

                // Forward event to subscribers (MainPage)
                PluginEventReceived?.Invoke(this, new PluginEventArgs { Event = evt });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the subscription
            _logger.LogDebug("Event subscription cancelled for plugin {PluginId}", worker.PluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event subscription for plugin {PluginId}", worker.PluginId);
        }
    }

    private void CleanupWorker(PluginWorkerInfo worker)
    {
        try
        {
            // Cancel event subscription
            worker.EventSubscriptionCts?.Cancel();
            worker.EventSubscriptionCts?.Dispose();

            worker.Channel?.Dispose();
            worker.Process?.Dispose();

            if (File.Exists(worker.SocketPath))
            {
                File.Delete(worker.SocketPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up worker for plugin {PluginId}", worker.PluginId);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();

        List<PluginWorkerInfo> workers;
        lock (_lock)
        {
            workers = _workers.Values.ToList();
            _workers.Clear();
        }

        _logger.LogInformation("Disposing PluginHostManager, terminating {Count} workers", workers.Count);

        foreach (var worker in workers)
        {
            try
            {
                // Dispose gRPC channel first
                worker.Channel?.Dispose();

                // Kill the process and wait for it to exit
                if (!worker.Process.HasExited)
                {
                    worker.Process.Kill(entireProcessTree: true);
                    worker.Process.WaitForExit(5000);
                }

                CleanupWorker(worker);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing worker for plugin {PluginId}", worker.PluginId);
            }
        }

        // Give OS time to release file handles after all processes are terminated
        Thread.Sleep(200);

        GC.SuppressFinalize(this);
    }
}

// ==== DTOs and Models ====

public record PluginInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public required string Category { get; init; }
    public string? Company { get; init; }
    public string? Icon { get; init; }
    public required IReadOnlyList<PluginCommand> Commands { get; init; }
    public required string UiEntry { get; init; }
    public string? UiModule { get; init; }
    public string? UiScope { get; init; }
    public bool IsRunning { get; init; }
}

public record PluginCommand
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
}

public record PluginInstanceInfo
{
    public required string PluginId { get; init; }
    public required string InstanceId { get; init; }
    public bool IsNew { get; init; }
}

internal record PluginWorkerInfo
{
    public required string PluginId { get; init; }
    public required string InstanceId { get; init; }
    public required Process Process { get; init; }
    public required PluginHostService.PluginHostServiceClient Client { get; init; }
    public required GrpcChannel Channel { get; init; }
    public required string SocketPath { get; init; }
    public CancellationTokenSource? EventSubscriptionCts { get; set; }
}

internal record PluginManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public string? Author { get; init; }
    public string? Category { get; init; }
    public string? Company { get; init; }
    public string? Icon { get; init; }
    public UiInfo? Ui { get; init; }
    public BackendInfo? Backend { get; init; }
}

internal record UiInfo
{
    public string? Entry { get; init; }
    public string? DevEntry { get; init; }
    public string? Module { get; init; }
    public string? Scope { get; init; }
}

internal record BackendInfo
{
    public string? Assembly { get; init; }
    public string? EntryPoint { get; init; }
}
