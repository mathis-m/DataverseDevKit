using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using DataverseDevKit.PluginHost.Contracts;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages out-of-process plugin worker lifecycle and communication.
/// </summary>
public sealed class PluginHostManager : IDisposable
{
    private readonly ILogger<PluginHostManager> _logger;
    private readonly StorageService _storageService;
    private readonly Dictionary<string, PluginWorkerInfo> _workers = new();
    private readonly string _pluginsBasePath;
    private readonly HttpClient _httpClient;

    public PluginHostManager(ILogger<PluginHostManager> logger, StorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
        
        // Plugins are located in the plugins/first-party directory
        var appPath = AppContext.BaseDirectory;
        _pluginsBasePath = Path.Combine(appPath, "..", "..", "..", "..", "..", "..", "..", "plugins", "first-party");
        _pluginsBasePath = Path.GetFullPath(_pluginsBasePath);

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        _logger.LogInformation("Plugins base path: {Path}", _pluginsBasePath);
    }

    private async Task<string> ResolveUiEntryUrlAsync(string pluginId, UiInfo? uiInfo)
    {
        if (uiInfo == null)
        {
            return string.Empty;
        }

        // In development mode, use devServer if available and running
        if (!string.IsNullOrEmpty(uiInfo.DevServer) && IsDevelopmentMode())
        {
            if (await IsDevServerRunningAsync(uiInfo.DevServer))
            {
                var remoteEntry = uiInfo.RemoteEntry ?? "remoteEntry.js";
                // In dev mode, Vite serves the built module federation assets from /dist/assets/
                var fileName = Path.GetFileName(remoteEntry);
                var devUrl = $"{uiInfo.DevServer.TrimEnd('/')}/dist/assets/{fileName}";
                _logger.LogInformation("Using dev server for plugin {PluginId}: {Url}", pluginId, devUrl);
                return devUrl;
            }
            else
            {
                _logger.LogWarning("Dev server not accessible for plugin {PluginId} at {DevServer}, falling back to production mode", pluginId, uiInfo.DevServer);
            }
        }

        // In production mode, use relative path served by backend
        if (!string.IsNullOrEmpty(uiInfo.RemoteEntry))
        {
            var relativePath = uiInfo.RemoteEntry.TrimStart('.', '/');
            // Files should be copied to wwwroot/plugins/{pluginId}/ui/ during build
            return $"plugins/{pluginId}/{relativePath}";
        }

        return string.Empty;
    }

    private bool IsDevelopmentMode()
    {
        // Check if we're in development mode (could use environment variable or config)
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        return isDev || Debugger.IsAttached; // Also consider debug mode as dev
    }

    private async Task<bool> IsDevServerRunningAsync(string devServerUrl)
    {
        try
        {
            // Make a HEAD request to check if the server is accessible
            using var request = new HttpRequestMessage(HttpMethod.Head, devServerUrl);
            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dev server check failed for {Url}", devServerUrl);
            return false;
        }
    }

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
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest != null)
                {
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
                        Commands = new List<PluginCommand>(), // Will be populated on demand
                        UiEntry = await ResolveUiEntryUrlAsync(manifest.Id, manifest.Ui),
                        IsRunning = _workers.ContainsKey(manifest.Id)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading plugin manifest: {Path}", manifestPath);
            }
        }

        return plugins;
    }

    public async Task<List<PluginCommand>> GetPluginCommandsAsync(string pluginId)
    {
        var worker = await EnsurePluginWorkerAsync(pluginId);
        
        var request = new GetCommandsRequest();
        var response = await worker.Client.GetCommandsAsync(request);

        return response.Commands.Select(c => new PluginCommand
        {
            Name = c.Name,
            Label = c.Label,
            Description = c.Description
        }).ToList();
    }

    public async Task<string> InvokePluginCommandAsync(string pluginId, string command, string payload)
    {
        var worker = await EnsurePluginWorkerAsync(pluginId);
        
        var correlationId = Guid.NewGuid().ToString();
        var request = new ExecuteRequest
        {
            CommandName = command,
            Payload = payload,
            CorrelationId = correlationId
        };

        _logger.LogInformation("Invoking plugin command: {PluginId}.{Command}", pluginId, command);

        var response = await worker.Client.ExecuteAsync(request);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Plugin command failed: {response.ErrorMessage}");
        }

        return response.Result;
    }

    private async Task<PluginWorkerInfo> EnsurePluginWorkerAsync(string pluginId)
    {
        if (_workers.TryGetValue(pluginId, out var existingWorker))
        {
            return existingWorker;
        }

        return await StartPluginWorkerAsync(pluginId);
    }

    private async Task<PluginWorkerInfo> StartPluginWorkerAsync(string pluginId)
    {
        _logger.LogInformation("Starting plugin worker: {PluginId}", pluginId);

        // Load manifest - find the plugin directory by searching for matching manifest
        string? pluginDir = null;
        string? manifestPath = null;

        if (Directory.Exists(_pluginsBasePath))
        {
            foreach (var dir in Directory.GetDirectories(_pluginsBasePath))
            {
                var testManifestPath = Path.Combine(dir, "plugin.manifest.json");
                if (File.Exists(testManifestPath))
                {
                    var testManifestJson = await File.ReadAllTextAsync(testManifestPath);
                    var testManifest = JsonSerializer.Deserialize<PluginManifest>(testManifestJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (testManifest?.Id == pluginId)
                    {
                        pluginDir = dir;
                        manifestPath = testManifestPath;
                        break;
                    }
                }
            }
        }
        
        if (pluginDir == null || manifestPath == null || !File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Plugin manifest not found for plugin: {pluginId}");
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (manifest?.Backend?.EntryAssembly == null && manifest?.Backend?.Assembly == null)
        {
            throw new InvalidOperationException($"Invalid plugin manifest for: {pluginId}");
        }

        // Locate plugin host executable
        var pluginHostExe = Path.Combine(AppContext.BaseDirectory, "DataverseDevKit.PluginHost.exe");
        
        if (!File.Exists(pluginHostExe))
        {
            pluginHostExe = Path.Combine(AppContext.BaseDirectory, "DataverseDevKit.PluginHost");
        }
        
        // If not in the same directory, look in the PluginRuntime build output (for development)
        if (!File.Exists(pluginHostExe))
        {
            var pluginRuntimePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "src", "dotnet", "PluginRuntime", "bin", "Debug");
            pluginRuntimePath = Path.GetFullPath(pluginRuntimePath);
            
            if (Directory.Exists(pluginRuntimePath))
            {
                // Search for the executable in all subdirectories
                var foundExes = Directory.GetFiles(pluginRuntimePath, "DataverseDevKit.PluginHost.exe", SearchOption.AllDirectories);
                if (foundExes.Length > 0)
                {
                    pluginHostExe = foundExes[0];
                    _logger.LogInformation("Found PluginHost at: {Path}", pluginHostExe);
                }
            }
        }
        
        if (!File.Exists(pluginHostExe))
        {
            throw new FileNotFoundException($"Plugin host executable not found. Expected at: {pluginHostExe}");
        }

        // Locate plugin assembly
        var assemblyName = manifest.Backend.Assembly ?? manifest.Backend.EntryAssembly;
        if (string.IsNullOrEmpty(assemblyName))
        {
            throw new InvalidOperationException($"Plugin manifest does not specify an assembly for: {pluginId}");
        }
        
        // First try in the plugin directory (for deployed plugins)
        var assemblyPath = Path.Combine(pluginDir, assemblyName);
        
        // If not found, search in the .NET build output directories (for development)
        if (!File.Exists(assemblyPath))
        {
            var dotnetPluginsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "src", "dotnet", "FirstPartyPlugins");
            dotnetPluginsPath = Path.GetFullPath(dotnetPluginsPath);
            
            if (Directory.Exists(dotnetPluginsPath))
            {
                // Search for the assembly in all subdirectories
                foreach (var dir in Directory.GetDirectories(dotnetPluginsPath))
                {
                    var binDir = Path.Combine(dir, "bin");
                    if (Directory.Exists(binDir))
                    {
                        var foundFiles = Directory.GetFiles(binDir, assemblyName, SearchOption.AllDirectories);
                        if (foundFiles.Length > 0)
                        {
                            // Prefer Debug build, then Release
                            assemblyPath = foundFiles.FirstOrDefault(f => f.Contains("Debug", StringComparison.OrdinalIgnoreCase)) ?? foundFiles[0];
                            break;
                        }
                    }
                }
            }
        }

        assemblyPath = Path.GetFullPath(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");
        }
        
        _logger.LogInformation("Found plugin assembly: {AssemblyPath}", assemblyPath);

        // Get storage path
        var storagePath = _storageService.GetPluginStoragePath(pluginId);

        // Start worker process
        var psi = new ProcessStartInfo
        {
            FileName = pluginHostExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment["DDK_PLUGIN_ID"] = pluginId;
        psi.Environment["DDK_PLUGIN_ASSEMBLY"] = assemblyPath;
        psi.Environment["DDK_TRANSPORT"] = "uds";

        var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start plugin worker process");
        }

        // Start reading stderr in background to capture any errors
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

        // Read socket path from stdout with timeout
        var socketPath = string.Empty;
        var readTask = process.StandardOutput.ReadLineAsync();
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(readTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            process.Kill();
            var errors = errorOutput.ToString();
            throw new InvalidOperationException($"Timeout waiting for socket path from plugin worker. Stderr: {errors}");
        }
        
        var outputLine = await readTask;
        
        if (outputLine?.StartsWith("SOCKET_PATH=") == true)
        {
            socketPath = outputLine.Substring("SOCKET_PATH=".Length);
            _logger.LogInformation("Plugin worker socket: {SocketPath}", socketPath);
        }
        else
        {
            process.Kill();
            var errors = errorOutput.ToString();
            throw new InvalidOperationException($"Failed to get socket path from plugin worker. Output: '{outputLine}'. Stderr: {errors}");
        }

        // Wait a moment for the server to start
        await Task.Delay(500);

        // Connect via gRPC
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = CreateUnixSocketHandler(socketPath)
        });

        var client = new PluginHostService.PluginHostServiceClient(channel);

        // Initialize the plugin
        var initRequest = new InitializeRequest
        {
            PluginId = pluginId,
            StoragePath = storagePath
        };

        var initResponse = await client.InitializeAsync(initRequest);

        if (!initResponse.Success)
        {
            process.Kill();
            throw new InvalidOperationException($"Plugin initialization failed: {initResponse.ErrorMessage}");
        }

        _logger.LogInformation("Plugin worker started: {PluginName} v{Version}", initResponse.PluginName, initResponse.PluginVersion);

        var worker = new PluginWorkerInfo
        {
            PluginId = pluginId,
            Process = process,
            Client = client,
            Channel = channel,
            SocketPath = socketPath
        };

        _workers[pluginId] = worker;

        return worker;
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

    public void Dispose()
    {
        _httpClient?.Dispose();
        
        // Clean up any running workers
        foreach (var worker in _workers.Values)
        {
            try
            {
                worker.Channel?.Dispose();
                if (!worker.Process.HasExited)
                {
                    worker.Process.Kill();
                }
                worker.Process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing worker for plugin {PluginId}", worker.PluginId);
            }
        }
        _workers.Clear();
        
        GC.SuppressFinalize(this);
    }
}

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
    public bool IsRunning { get; init; }
}

public record PluginCommand
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
}

internal record PluginWorkerInfo
{
    public required string PluginId { get; init; }
    public required Process Process { get; init; }
    public required PluginHostService.PluginHostServiceClient Client { get; init; }
    public required GrpcChannel Channel { get; init; }
    public required string SocketPath { get; init; }
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

internal record AuthorInfo
{
    public string? Name { get; init; }
}

internal record UiInfo
{
    public string? Entry { get; init; }
    public string? RemoteEntry { get; init; }
    public string? DevServer { get; init; }
    public string? Module { get; init; }
    public string? Scope { get; init; }
}

internal record BackendInfo
{
    public string? Assembly { get; init; }
    public string? EntryPoint { get; init; }
    public string? EntryAssembly { get; init; } // Legacy support
}
