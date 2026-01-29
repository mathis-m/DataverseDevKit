using Grpc.Core;
using Microsoft.Extensions.Logging;
using DataverseDevKit.PluginHost.Contracts;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// gRPC service that plugins call to get access tokens.
/// Runs in the host process, provides a secure callback channel for plugins.
/// </summary>
public class TokenProviderGrpcService : TokenProviderHostService.TokenProviderHostServiceBase
{
    private readonly ILogger<TokenProviderGrpcService> _logger;
    private readonly TokenProviderService _tokenProvider;
    private readonly ConnectionService _connectionService;

    public TokenProviderGrpcService(
        ILogger<TokenProviderGrpcService> logger,
        TokenProviderService tokenProvider,
        ConnectionService connectionService)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _connectionService = connectionService;
    }

    public override async Task<GetAccessTokenResponse> GetAccessToken(
        GetAccessTokenRequest request,
        ServerCallContext context)
    {
        try
        {
            _logger.LogDebug("Plugin requesting access token for connection: {ConnectionId}", 
                string.IsNullOrEmpty(request.ConnectionId) ? "(active)" : request.ConnectionId);

            var connectionId = string.IsNullOrEmpty(request.ConnectionId) ? null : request.ConnectionId;
            var result = await _tokenProvider.GetAccessTokenAsync(connectionId, context.CancellationToken);

            return new GetAccessTokenResponse
            {
                Success = true,
                AccessToken = result.AccessToken,
                ExpiresAtUnix = result.ExpiresOn.ToUnixTimeSeconds()
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Token request failed - not authenticated");
            return new GetAccessTokenResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token request failed with unexpected error");
            return new GetAccessTokenResponse
            {
                Success = false,
                ErrorMessage = $"Failed to get access token: {ex.Message}"
            };
        }
    }

    public override async Task<GetConnectionInfoResponse> GetConnectionInfo(
        GetConnectionInfoRequest request,
        ServerCallContext context)
    {
        try
        {
            var connection = await _connectionService.GetActiveConnectionAsync();
            
            if (connection == null)
            {
                return new GetConnectionInfoResponse
                {
                    ConnectionId = string.Empty,
                    ConnectionName = string.Empty,
                    ConnectionUrl = string.Empty,
                    IsAuthenticated = false
                };
            }

            return new GetConnectionInfoResponse
            {
                ConnectionId = connection.Id,
                ConnectionName = connection.Name,
                ConnectionUrl = connection.Url,
                IsAuthenticated = connection.IsAuthenticated,
                UserName = connection.AuthenticatedUser ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection info");
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }
}
