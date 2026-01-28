using Microsoft.PowerPlatform.Dataverse.Client;

namespace DataverseDevKit.Core.Abstractions;

/// <summary>
/// Factory for creating Dataverse ServiceClient instances per connection.
/// </summary>
public interface IServiceClientFactory
{
    /// <summary>
    /// Gets a ServiceClient for the specified connection ID.
    /// </summary>
    /// <param name="connectionId">The connection ID, or null for the active connection.</param>
    /// <returns>A ServiceClient instance for the connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not found or not authenticated.</exception>
    ServiceClient GetServiceClient(string? connectionId = null);
}

