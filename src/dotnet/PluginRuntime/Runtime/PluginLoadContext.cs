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
        // Try to load shared assemblies from the host's directory (not plugin's)
        // This ensures plugins use the host's version of shared interfaces/abstractions
        if (IsSharedAssembly(assemblyName.Name))
        {
            // First, check if it's already loaded in the default context
            var loadedAssembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
            
            if (loadedAssembly != null)
            {
                Debug.WriteLine($"[PluginLoadContext] Using already-loaded {assemblyName.Name} from default context");
                return loadedAssembly;
            }

            // Try to load from host directory (where PluginHost.exe is)
            var hostAssemblyPath = Path.Combine(_hostDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(hostAssemblyPath))
            {
                Debug.WriteLine($"[PluginLoadContext] Loading {assemblyName.Name} from host directory: {hostAssemblyPath}");
                // Load into default context so it's shared
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(hostAssemblyPath);
            }
            
            Debug.WriteLine($"[PluginLoadContext] {assemblyName.Name} not found in host, will try plugin directory");
        }

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

    private static bool IsSharedAssembly(string? assemblyName)
    {
        if (assemblyName == null) return false;
        
        return assemblyName == "DataverseDevKit.Shared" ||
               assemblyName == "DataverseDevKit.Contracts" ||
               assemblyName.StartsWith("Microsoft.Extensions.Logging");
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
