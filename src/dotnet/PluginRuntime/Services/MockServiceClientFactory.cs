using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;

namespace DataverseDevKit.PluginHost.Services;

/// <summary>
/// Mock implementation of IServiceClientFactory for development/testing.
/// In production, this would create real ServiceClient instances connected to Dataverse.
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
        _logger.LogInformation("MockServiceClientFactory: Creating mock ServiceClient for connection: {ConnectionId}", connectionId ?? "default");
        
        // In a real implementation, this would:
        // 1. Look up connection details from ConnectionService
        // 2. Create a ServiceClient with proper credentials
        // 3. Return the authenticated client
        
        // For now, return a mock client with a connection string that won't actually connect
        // This allows the code to run without real Dataverse connectivity
        var mockConnectionString = "AuthType=OAuth;Url=https://mock.crm.dynamics.com;Username=mock@test.com;Password=mock;AppId=mock;RedirectUri=http://localhost;LoginPrompt=Never";
        
        // Note: This will fail if actually used, but allows code compilation and structure testing
        // In real usage, plugins should use the actual connection details from the host
        return new ServiceClient(mockConnectionString);
    }
}
