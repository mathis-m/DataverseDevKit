using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseDevKit.PluginHost.Services;

namespace DataverseDevKit.PluginHost.Tests;

/// <summary>
/// Tests for DataverseClientMultiplexer connection pooling functionality.
/// </summary>
public class DataverseClientMultiplexerTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILoggerFactory _loggerFactory;

    public DataverseClientMultiplexerTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    [Fact]
    public void RegisterEnvironment_RegistersNewEnvironment()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);
        var instanceUrl = "https://test.crm.dynamics.com";

        // Act
        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        // Assert - Should not throw
        var client = multiplexer.GetServiceClient(instanceUrl);
        Assert.NotNull(client);

        // Cleanup
        client.Dispose();
        multiplexer.Dispose();
    }

    [Fact]
    public void RegisterEnvironment_AllowsMultipleCallsForSameEnvironment()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);
        var instanceUrl = "https://test.crm.dynamics.com";

        // Act
        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));
        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl)); // Should not throw

        // Assert
        var client = multiplexer.GetServiceClient(instanceUrl);
        Assert.NotNull(client);

        // Cleanup
        client.Dispose();
        multiplexer.Dispose();
    }

    [Fact]
    public void GetServiceClient_ClonesRootInstance()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        // Act
        var client1 = multiplexer.GetServiceClient(instanceUrl);
        var client2 = multiplexer.GetServiceClient(instanceUrl);

        // Assert
        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2); // Should be different instances

        // Cleanup
        client1.Dispose();
        client2.Dispose();
        multiplexer.Dispose();
    }

    [Fact]
    public void GetServiceClient_ThrowsForUnregisteredEnvironment()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            multiplexer.GetServiceClient("https://unregistered.crm.dynamics.com"));

        // Cleanup
        multiplexer.Dispose();
    }

    [Fact]
    public async Task GetMultiplexedClientAsync_ReturnsLeasedClient()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger, maxConcurrencyPerEnvironment: 5);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        // Act
        using var leasedClient = await multiplexer.GetMultiplexedClientAsync(instanceUrl);

        // Assert
        Assert.NotNull(leasedClient);
        Assert.NotNull(leasedClient.Client);

        // Cleanup
        multiplexer.Dispose();
    }

    [Fact]
    public async Task GetMultiplexedClientAsync_ReusesPooledClients()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger, maxConcurrencyPerEnvironment: 2);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        // Act
        ServiceClient? firstClient = null;
        ServiceClient? secondClient = null;

        // Get and return a client
        using (var leased1 = await multiplexer.GetMultiplexedClientAsync(instanceUrl))
        {
            firstClient = leased1.Client;
        }

        // Get another client - should reuse the first one from the pool
        using (var leased2 = await multiplexer.GetMultiplexedClientAsync(instanceUrl))
        {
            secondClient = leased2.Client;
        }

        // Assert
        Assert.NotNull(firstClient);
        Assert.NotNull(secondClient);
        Assert.Same(firstClient, secondClient); // Should be the same instance (reused from pool)

        // Cleanup
        multiplexer.Dispose();
    }

    [Fact]
    public async Task GetMultiplexedClientAsync_RespectsConcurrencyLimit()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var maxConcurrency = 2;
        var multiplexer = new DataverseClientMultiplexer(logger, maxConcurrencyPerEnvironment: maxConcurrency);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        var acquiredClients = new List<LeasedServiceClient>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Acquire max number of clients
        for (int i = 0; i < maxConcurrency; i++)
        {
            var client = await multiplexer.GetMultiplexedClientAsync(instanceUrl, cts.Token);
            acquiredClients.Add(client);
        }

        // Try to acquire one more - should timeout because we've hit the limit
        var acquireTask = multiplexer.GetMultiplexedClientAsync(instanceUrl, cts.Token);
        var delayTask = Task.Delay(500);
        var completedTask = await Task.WhenAny(acquireTask, delayTask);

        // Assert
        Assert.Same(delayTask, completedTask); // Should timeout, not acquire
        Assert.Equal(maxConcurrency, acquiredClients.Count);

        // Cleanup
        foreach (var client in acquiredClients)
        {
            client.Dispose();
        }
        multiplexer.Dispose();
    }

    [Fact]
    public async Task GetMultiplexedClientAsync_ThrowsForUnregisteredEnvironment()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await multiplexer.GetMultiplexedClientAsync("https://unregistered.crm.dynamics.com"));

        // Cleanup
        multiplexer.Dispose();
    }

    [Fact]
    public void Dispose_DisposesAllClients()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));
        var client = multiplexer.GetServiceClient(instanceUrl);

        // Act
        multiplexer.Dispose();

        // Assert - Should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => 
            multiplexer.GetServiceClient(instanceUrl));

        // Cleanup
        client.Dispose();
    }

    [Fact]
    public async Task GetMultiplexedClientAsync_AllowsConcurrentAccess()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger<DataverseClientMultiplexer>();
        var multiplexer = new DataverseClientMultiplexer(logger, maxConcurrencyPerEnvironment: 10);
        var instanceUrl = "https://test.crm.dynamics.com";

        multiplexer.RegisterEnvironment(instanceUrl, () => CreateMockServiceClient(instanceUrl));

        // Act - Create multiple concurrent tasks
        var tasks = new List<Task>();
        var successCount = 0;
        var lockObj = new object();

        for (int i = 0; i < 20; i++)
        {
            var task = Task.Run(async () =>
            {
                using var leased = await multiplexer.GetMultiplexedClientAsync(instanceUrl);
                Assert.NotNull(leased.Client);
                
                // Simulate some work
                await Task.Delay(10);
                
                lock (lockObj)
                {
                    successCount++;
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, successCount);

        // Cleanup
        multiplexer.Dispose();
    }

    private ServiceClient CreateMockServiceClient(string instanceUrl)
    {
        var logger = _loggerFactory.CreateLogger<MockServiceClientFactory>();
        var uri = new Uri(instanceUrl);
        return new ServiceClient(uri, (resource) => Task.FromResult("mock-token"), useUniqueInstance: true, logger: logger);
    }
}
