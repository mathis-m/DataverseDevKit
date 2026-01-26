using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Loads and manages plugin assemblies.
/// </summary>
public class PluginLoader
{
    private ILogger _logger = NullLogger.Instance;
    private IToolPlugin? _plugin;
    private IPluginContext? _context;

    public IToolPlugin Plugin => _plugin ?? throw new InvalidOperationException("Plugin not loaded");
    public IPluginContext Context => _context ?? throw new InvalidOperationException("Plugin not initialized");

    /// <summary>
    /// Sets the logger for this loader. Should be called after DI is available.
    /// </summary>
    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public async Task LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading plugin assembly from: {AssemblyPath}", assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");
        }

        // Load assembly
        var assembly = Assembly.LoadFrom(assemblyPath);
        _logger.LogInformation("Loaded assembly: {AssemblyName}", assembly.FullName);

        // Find plugin type
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IToolPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (pluginType == null)
        {
            throw new InvalidOperationException($"No IToolPlugin implementation found in assembly: {assemblyPath}");
        }

        _logger.LogInformation("Found plugin type: {PluginType}", pluginType.FullName);

        // Create plugin instance
        _plugin = (IToolPlugin?)Activator.CreateInstance(pluginType);
        if (_plugin == null)
        {
            throw new InvalidOperationException($"Failed to create instance of plugin type: {pluginType.FullName}");
        }

        _logger.LogInformation("Plugin instance created: {PluginId}", _plugin.PluginId);
    }

    public async Task InitializePluginAsync(string pluginId, string storagePath, Dictionary<string, string> config, ILogger contextLogger, IServiceClientFactory serviceClientFactory, CancellationToken cancellationToken = default)
    {
        if (_plugin == null)
        {
            throw new InvalidOperationException("Plugin not loaded");
        }

        _logger.LogInformation("Initializing plugin: {PluginId}", pluginId);

        // Create context with provided logger and service client factory
        _context = new PluginContextImpl(contextLogger, storagePath, serviceClientFactory);

        // Initialize plugin
        await _plugin.InitializeAsync(_context, cancellationToken);

        _logger.LogInformation("Plugin initialized successfully");
    }

    public async Task DisposeAsync()
    {
        if (_plugin != null)
        {
            await _plugin.DisposeAsync();
        }
    }
}
