using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DataverseDevKit.PluginHost.Services;

/// <summary>
/// Manages connection pooling for Dataverse ServiceClient instances per environment.
/// Provides thread-safe access to clients through cloning or multiplexed leasing.
/// </summary>
public sealed class DataverseClientMultiplexer : IDisposable
{
    private sealed class EnvEntry : IDisposable
    {
        public ServiceClient Root { get; }
        public ConcurrentBag<ServiceClient> Pool { get; } = new();
        public SemaphoreSlim Gate { get; }

        public EnvEntry(ServiceClient root, int maxConcurrency)
        {
            Root = root;
            Gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        public void Dispose()
        {
            while (Pool.TryTake(out var c))
                c.Dispose();
            Root.Dispose();
            Gate.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, EnvEntry> _environments = new();
    private readonly ILogger _logger;
    private readonly int _maxConcurrencyPerEnvironment;
    private bool _disposed;

    public DataverseClientMultiplexer(ILogger logger, int maxConcurrencyPerEnvironment = 10)
    {
        _logger = logger;
        _maxConcurrencyPerEnvironment = maxConcurrencyPerEnvironment;
    }

    /// <summary>
    /// Registers or gets a root ServiceClient for an environment.
    /// </summary>
    /// <param name="instanceUrl">The Dataverse instance URL</param>
    /// <param name="rootClientFactory">Factory function to create the root client if not exists</param>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "String is used for consistency with existing code")]
    public void RegisterEnvironment(string instanceUrl, Func<ServiceClient> rootClientFactory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        _environments.GetOrAdd(instanceUrl, key =>
        {
            _logger.LogInformation("Registering new environment: {InstanceUrl}", instanceUrl);
            var root = rootClientFactory();
            return new EnvEntry(root, _maxConcurrencyPerEnvironment);
        });
    }

    /// <summary>
    /// Gets a cloned ServiceClient instance for the default method.
    /// This creates a thread-safe clone of the root instance.
    /// </summary>
    /// <param name="instanceUrl">The Dataverse instance URL</param>
    /// <returns>A cloned ServiceClient instance</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "String is used for consistency with existing code")]
    public ServiceClient GetServiceClient(string instanceUrl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (!_environments.TryGetValue(instanceUrl, out var entry))
        {
            throw new InvalidOperationException($"Environment not registered: {instanceUrl}");
        }

        _logger.LogDebug("Cloning ServiceClient for {InstanceUrl}", instanceUrl);
        
        // Thread-safe clone of the root instance
        return entry.Root.Clone();
    }

    /// <summary>
    /// Gets a multiplexed ServiceClient that is leased from the pool.
    /// The client is leased from the pool and occupies a concurrency slot.
    /// It must be disposed to return it to the pool and release the slot.
    /// </summary>
    /// <param name="instanceUrl">The Dataverse instance URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A disposable wrapper containing the leased client</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "String is used for consistency with existing code")]
    public async Task<LeasedServiceClient> GetMultiplexedClientAsync(string instanceUrl, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (!_environments.TryGetValue(instanceUrl, out var entry))
        {
            throw new InvalidOperationException($"Environment not registered: {instanceUrl}");
        }

        // Wait for available slot
        await entry.Gate.WaitAsync(cancellationToken);

        try
        {
            ServiceClient client;
            
            // Try to get from pool first
            if (entry.Pool.TryTake(out var pooledClient))
            {
                _logger.LogDebug("Leased pooled ServiceClient for {InstanceUrl}", instanceUrl);
                client = pooledClient;
            }
            else
            {
                // Create new clone if pool is empty
                _logger.LogDebug("Creating new ServiceClient clone for {InstanceUrl}", instanceUrl);
                client = entry.Root.Clone();
            }

            return new LeasedServiceClient(client, entry.Pool, entry.Gate);
        }
        catch
        {
            // Release the semaphore if we fail to get a client
            entry.Gate.Release();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger.LogInformation("Disposing DataverseClientMultiplexer");

        foreach (var entry in _environments.Values)
        {
            entry.Dispose();
        }

        _environments.Clear();
        GC.SuppressFinalize(this);
    }
}
