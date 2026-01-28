using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Custom AssemblyLoadContext that handles plugin assembly isolation and dependency resolution.
/// Each plugin gets its own load context to avoid assembly conflicts between plugins.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;
    private readonly string _hostDirectory;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? throw new ArgumentException("Invalid plugin path", nameof(pluginPath));
        _hostDirectory = AppContext.BaseDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Strategy: If an assembly is already loaded in the default context, use it.
        // This ensures that shared assemblies (host dependencies) are automatically
        // shared with plugins, preventing type identity mismatches.
        // Only load plugin-specific assemblies into the isolated plugin context.
        
        // First, check if the assembly is already loaded in the default context
        var loadedAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        
        if (loadedAssembly != null)
        {
            Debug.WriteLine($"[PluginLoadContext] Using already-loaded {assemblyName.Name} from default context (shared assembly)");
            return loadedAssembly;
        }

        // If not already loaded, try to load from host directory
        // This covers cases where the host has the assembly but hasn't loaded it yet
        var hostAssemblyPath = Path.Combine(_hostDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(hostAssemblyPath))
        {
            Debug.WriteLine($"[PluginLoadContext] Loading {assemblyName.Name} from host directory into default context: {hostAssemblyPath}");
            // Load into default context so it becomes shared
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(hostAssemblyPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginLoadContext] Failed to load {assemblyName.Name} from host directory: {ex.Message}");
                // Continue to try plugin directory
            }
        }

        // Assembly not in host, so it's plugin-specific. Load into plugin's isolated context.
        
        // Try to resolve using the dependency resolver (uses .deps.json)
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            Debug.WriteLine($"[PluginLoadContext] Resolved {assemblyName.Name} via deps.json to: {assemblyPath}");
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fallback: probe the plugin directory for the assembly
        var probePaths = new[]
        {
            Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll"),
            Path.Combine(_pluginDirectory, assemblyName.Name + ".exe")
        };

        foreach (var probePath in probePaths)
        {
            if (File.Exists(probePath))
            {
                Debug.WriteLine($"[PluginLoadContext] Resolved {assemblyName.Name} via probing to: {probePath}");
                return LoadFromAssemblyPath(probePath);
            }
        }

        // Let the default context handle remaining assemblies (system, runtime, etc.)
        Debug.WriteLine($"[PluginLoadContext] Delegating {assemblyName.Name} to default context");
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // Try to resolve native dependencies using the resolver
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            Debug.WriteLine($"[PluginLoadContext] Resolved native library {unmanagedDllName} via deps.json to: {libraryPath}");
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        // Probe the plugin directory for native libraries
        var probePaths = new[]
        {
            Path.Combine(_pluginDirectory, unmanagedDllName),
            Path.Combine(_pluginDirectory, $"{unmanagedDllName}.dll"),
            Path.Combine(_pluginDirectory, $"lib{unmanagedDllName}.so"),
            Path.Combine(_pluginDirectory, $"lib{unmanagedDllName}.dylib")
        };

        foreach (var probePath in probePaths)
        {
            if (File.Exists(probePath))
            {
                Debug.WriteLine($"[PluginLoadContext] Resolved native library {unmanagedDllName} via probing to: {probePath}");
                return LoadUnmanagedDllFromPath(probePath);
            }
        }

        Debug.WriteLine($"[PluginLoadContext] Could not resolve native library: {unmanagedDllName}");
        return IntPtr.Zero;
    }
}
