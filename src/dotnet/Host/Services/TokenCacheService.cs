using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages encrypted token cache persistence for MSAL.
/// Uses platform-specific encryption (DPAPI on Windows, Keychain on macOS).
/// </summary>
public class TokenCacheService
{
    private readonly ILogger<TokenCacheService> _logger;
    private readonly string _cacheDirectory;
    private readonly string _cacheFileName = "msal_token_cache.bin";
    private MsalCacheHelper? _cacheHelper;

    // KeyChain configuration for macOS
    private const string KeyChainServiceName = "DataverseDevKit";
    private const string KeyChainAccountName = "MsalTokenCache";

    // Linux keyring configuration
    private const string LinuxKeyRingSchema = "io.github.mathis-m.dataversedevkit";
    private const string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
    private const string LinuxKeyRingLabel = "MSAL Token Cache";
    private static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new("Version", "1");
    private static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new("ProductGroup", "DataverseDevKit");

    public TokenCacheService(ILogger<TokenCacheService> logger)
    {
        _logger = logger;

        // Determine cache directory based on platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DataverseDevKit");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "DataverseDevKit");
        }
        else
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "DataverseDevKit");
        }

        // Ensure directory exists
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation("Created token cache directory: {CacheDirectory}", _cacheDirectory);
        }
    }

    /// <summary>
    /// Registers the token cache with an MSAL application.
    /// Uses MSAL Extensions library for cross-platform encrypted storage.
    /// </summary>
    public async Task RegisterCacheAsync(IPublicClientApplication app)
    {
        try
        {
            var storageProperties = BuildStorageProperties();
            _cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            _cacheHelper.RegisterCache(app.UserTokenCache);

            _logger.LogInformation("Token cache registered successfully");
        }
        catch (MsalCachePersistenceException ex)
        {
            _logger.LogWarning(ex, "Failed to enable encrypted token cache, falling back to unencrypted storage");
            
            // Fall back to unencrypted storage (still in protected directory)
            var storageProperties = new StorageCreationPropertiesBuilder(
                _cacheFileName,
                _cacheDirectory)
                .WithUnprotectedFile()
                .Build();

            _cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            _cacheHelper.RegisterCache(app.UserTokenCache);
        }
    }

    private StorageCreationProperties BuildStorageProperties()
    {
        var builder = new StorageCreationPropertiesBuilder(
            _cacheFileName,
            _cacheDirectory);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            builder.WithMacKeyChain(
                KeyChainServiceName,
                KeyChainAccountName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            builder.WithLinuxKeyring(
                LinuxKeyRingSchema,
                LinuxKeyRingCollection,
                LinuxKeyRingLabel,
                LinuxKeyRingAttr1,
                LinuxKeyRingAttr2);
        }
        // On Windows, DPAPI is used automatically

        return builder.Build();
    }

    /// <summary>
    /// Clears all cached tokens.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        var cachePath = Path.Combine(_cacheDirectory, _cacheFileName);
        
        if (File.Exists(cachePath))
        {
            try
            {
                File.Delete(cachePath);
                _logger.LogInformation("Token cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear token cache");
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the path to the token cache file for diagnostics.
    /// </summary>
    public string GetCachePath() => Path.Combine(_cacheDirectory, _cacheFileName);

    /// <summary>
    /// Checks if a cached token exists (without decrypting/loading it).
    /// </summary>
    public bool HasCachedTokens()
    {
        var cachePath = Path.Combine(_cacheDirectory, _cacheFileName);
        return File.Exists(cachePath) && new FileInfo(cachePath).Length > 0;
    }
}
