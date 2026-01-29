using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Loads and manages plugin assemblies with automatic dependency resolution.
/// </summary>
public class PluginLoader
{
    private ILogger _logger = NullLogger.Instance;
    private IToolPlugin? _plugin;
    private IPluginContext? _context;
    private PluginLoadContext? _loadContext;

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

        // Create isolated load context for the plugin
        // This automatically handles dependency resolution using .deps.json
        _loadContext = new PluginLoadContext(assemblyPath);
        _logger.LogInformation("Created isolated load context for plugin");

        // Load assembly in the isolated context
        var assembly = _loadContext.LoadFromAssemblyPath(assemblyPath);
        _logger.LogInformation("Loaded assembly: {AssemblyName}", assembly.FullName);

        // Find plugin type - use simple IsAssignableFrom since shared abstractions are from default context
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IToolPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (pluginType == null)
        {
            _logger.LogWarning("Available types in assembly: {Types}", 
                string.Join(", ", assembly.GetTypes().Select(t => t.FullName)));
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
        _context = new PluginContextImpl(contextLogger, storagePath, pluginId, serviceClientFactory);

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

        // Unload the plugin's load context to release all assemblies and their dependencies
        if (_loadContext != null)
        {
            _loadContext.Unload();
            _logger.LogInformation("Unloaded plugin assembly context");
        }
    }
}
