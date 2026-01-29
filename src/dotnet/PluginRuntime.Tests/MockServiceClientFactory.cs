using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Tests;

/// <summary>
/// Mock implementation of IServiceClientFactory for testing purposes.
/// </summary>
public class MockServiceClientFactory : IServiceClientFactory
{
    private readonly ILogger _logger;

    public MockServiceClientFactory(ILogger logger)
    {
        _logger = logger;
    }

    public ServiceClient GetServiceClient(string? connectionId = null)
    {
        _logger.LogDebug("MockServiceClientFactory: GetServiceClient called with connectionId: {ConnectionId}", connectionId ?? "null");
        
        // Create a mock ServiceClient instance
        // Using the constructor that doesn't require actual connection
        var mockUri = new Uri("https://mock.crm.dynamics.com");
        var mockClient = new ServiceClient(mockUri, (resource) => Task.FromResult("mock-token"), useUniqueInstance: true, logger: _logger);
        
        return mockClient;
    }
}
