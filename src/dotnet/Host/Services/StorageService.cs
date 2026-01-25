using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Provides isolated storage for plugins.
/// </summary>
public class StorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly string _basePath;

    public StorageService(ILogger<StorageService> logger)
    {
        _logger = logger;
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataverseDevKit",
            "Storage");

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public string GetPluginStoragePath(string pluginId)
    {
        var pluginPath = Path.Combine(_basePath, pluginId);
        
        if (!Directory.Exists(pluginPath))
        {
            Directory.CreateDirectory(pluginPath);
        }

        return pluginPath;
    }

    public async Task<string?> GetAsync(string pluginId, string key)
    {
        var filePath = Path.Combine(GetPluginStoragePath(pluginId), $"{key}.txt");
        
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath);
    }

    public async Task<bool> SetAsync(string pluginId, string key, string value)
    {
        var filePath = Path.Combine(GetPluginStoragePath(pluginId), $"{key}.txt");
        
        await File.WriteAllTextAsync(filePath, value);
        
        return true;
    }
}
