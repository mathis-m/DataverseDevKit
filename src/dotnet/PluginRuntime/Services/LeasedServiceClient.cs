using Microsoft.PowerPlatform.Dataverse.Client;
using System.Collections.Concurrent;

namespace DataverseDevKit.PluginHost.Services;

/// <summary>
/// Wrapper for a leased ServiceClient that automatically returns it to the pool when disposed.
/// </summary>
public sealed class LeasedServiceClient : IDisposable
{
    private readonly ServiceClient _client;
    private readonly ConcurrentBag<ServiceClient> _pool;
    private readonly SemaphoreSlim _gate;
    private int _disposed; // 0 = false, 1 = true

    internal LeasedServiceClient(ServiceClient client, ConcurrentBag<ServiceClient> pool, SemaphoreSlim gate)
    {
        _client = client;
        _pool = pool;
        _gate = gate;
    }

    public ServiceClient Client => _client;

    public void Dispose()
    {
        // Use Interlocked.Exchange for thread-safe disposal check
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Return client to pool
        _pool.Add(_client);
        
        // Release the semaphore slot
        _gate.Release();
    }
}
