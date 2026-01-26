using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Factory for creating Dataverse clients in the plugin runtime.
/// This is configured by the Host application at startup.
/// </summary>
public static class DataverseClientFactory
{
    private static Func<string?, ILogger, IDataverseClient>? _factory;

    /// <summary>
    /// Configures the factory with a client creator function.
    /// </summary>
    /// <param name="factory">Factory function that creates IDataverseClient instances.</param>
    public static void Configure(Func<string?, ILogger, IDataverseClient> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a Dataverse client for the specified connection.
    /// </summary>
    /// <param name="connectionId">Connection ID, or null for active connection.</param>
    /// <param name="logger">Logger for the client.</param>
    /// <returns>A Dataverse client instance.</returns>
    public static IDataverseClient CreateClient(string? connectionId, ILogger logger)
    {
        if (_factory == null)
        {
            throw new InvalidOperationException("DataverseClientFactory not configured. Call Configure() at startup.");
        }

        return _factory(connectionId, logger);
    }
}
