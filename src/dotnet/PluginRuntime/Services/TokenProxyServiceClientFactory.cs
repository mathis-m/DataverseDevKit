using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.PluginHost.Contracts;

namespace DataverseDevKit.PluginHost.Services;

/// <summary>
/// IServiceClientFactory implementation that proxies token requests to the host.
/// Uses the ServiceClient constructor that accepts a token provider delegate,
/// allowing the host to manage all authentication while plugins just use the client.
/// Supports connection pooling via DataverseClientMultiplexer.
/// </summary>
public class TokenProxyServiceClientFactory : IServiceClientFactory, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly TokenProviderHostService.TokenProviderHostServiceClient _tokenClient;
    private readonly GrpcChannel _channel;
    private readonly Uri _connectionUrl;
    private readonly string _connectionId;
    private readonly DataverseClientMultiplexer? _multiplexer;
    private bool _multiplexerInitialized;
    private readonly object _multiplexerLock = new();

    public TokenProxyServiceClientFactory(
        ILogger logger,
        string tokenCallbackSocket,
        Uri connectionUrl,
        string connectionId,
        bool enableConnectionPooling = true,
        int maxConcurrencyPerEnvironment = 10)
    {
        _logger = logger;
        _connectionUrl = connectionUrl;
        _connectionId = connectionId;

        // Connect to the host's token provider service
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = CreateUnixSocketHandler(tokenCallbackSocket)
        });
        
        _tokenClient = new TokenProviderHostService.TokenProviderHostServiceClient(_channel);
        
        _logger.LogInformation("TokenProxyServiceClientFactory initialized. Connection: {Url}, Pooling: {Pooling}", 
            connectionUrl, enableConnectionPooling);

        // Initialize multiplexer for connection pooling if enabled
        if (enableConnectionPooling)
        {
            _multiplexer = new DataverseClientMultiplexer(_logger, maxConcurrencyPerEnvironment);
        }
    }

    public ServiceClient GetServiceClient(string? connectionId = null)
    {
        // Normalize "default" to null to use the factory's configured connection
        var targetConnectionId = string.IsNullOrEmpty(connectionId) || connectionId.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? _connectionId
            : connectionId;

        _logger.LogDebug("Creating ServiceClient for {Uri} with token proxy (connection: {ConnectionId})", 
            _connectionUrl, targetConnectionId);

        // If multiplexer is enabled, use it for connection pooling
        if (_multiplexer != null)
        {
            EnsureMultiplexerInitialized(targetConnectionId);
            return _multiplexer.GetServiceClient(_connectionUrl.ToString());
        }

        // Fallback to creating a new ServiceClient directly
        return CreateServiceClient(targetConnectionId);
    }

    /// <summary>
    /// Gets a multiplexed ServiceClient that is leased from the pool.
    /// The client is locked and must be disposed to return it to the pool.
    /// This method requires connection pooling to be enabled.
    /// </summary>
    /// <param name="connectionId">The connection ID, or null for the active connection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A disposable wrapper containing the leased client</returns>
    public async Task<LeasedServiceClient> GetMultiplexedClientAsync(string? connectionId = null, CancellationToken cancellationToken = default)
    {
        if (_multiplexer == null)
        {
            throw new InvalidOperationException("Connection pooling is not enabled. Cannot get multiplexed client.");
        }

        // Normalize "default" to null to use the factory's configured connection
        var targetConnectionId = string.IsNullOrEmpty(connectionId) || connectionId.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? _connectionId
            : connectionId;

        EnsureMultiplexerInitialized(targetConnectionId);
        return await _multiplexer.GetMultiplexedClientAsync(_connectionUrl.ToString(), cancellationToken);
    }

    private void EnsureMultiplexerInitialized(string targetConnectionId)
    {
        if (_multiplexerInitialized)
            return;

        lock (_multiplexerLock)
        {
            if (_multiplexerInitialized)
                return;

            _logger.LogDebug("Initializing multiplexer for {Url}", _connectionUrl);
            
            // Register the environment with a root client
            _multiplexer!.RegisterEnvironment(_connectionUrl.ToString(), () => CreateServiceClient(targetConnectionId));
            _multiplexerInitialized = true;
        }
    }

    private ServiceClient CreateServiceClient(string targetConnectionId)
    {
        // Create ServiceClient with token provider delegate
        // This constructor is: ServiceClient(Uri, Func<string, Task<string>>, bool, ILogger)
        // The delegate receives the resource URL and returns the access token
        return new ServiceClient(
            _connectionUrl,
            async (resourceUrl) => await GetTokenFromHostAsync(targetConnectionId, resourceUrl),
            useUniqueInstance: true,
            logger: _logger);
    }

    private async Task<string> GetTokenFromHostAsync(string? connectionId, string resource)
    {
        _logger.LogDebug("Requesting token from host for resource: {Resource}", resource);

        var request = new GetAccessTokenRequest
        {
            ConnectionId = connectionId ?? string.Empty,
            Resource = resource
        };

        var response = await _tokenClient.GetAccessTokenAsync(request);

        if (!response.Success)
        {
            _logger.LogError("Failed to get access token: {Error}", response.ErrorMessage);
            throw new InvalidOperationException($"Failed to get access token: {response.ErrorMessage}");
        }

        _logger.LogDebug("Token received, expires at: {Expiry}", 
            DateTimeOffset.FromUnixTimeSeconds(response.ExpiresAtUnix));

        return response.AccessToken;
    }

    private static SocketsHttpHandler CreateUnixSocketHandler(string socketPath)
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);

                var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
                await socket.ConnectAsync(endpoint, cancellationToken);

                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _multiplexer?.Dispose();
        _channel?.Dispose();
        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}
