using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages authentication state and provides auth operations to the frontend.
/// Delegates actual token management to TokenProviderService.
/// </summary>
public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly TokenProviderService _tokenProvider;

    public AuthService(ILogger<AuthService> logger, TokenProviderService tokenProvider)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Initiates interactive OAuth login for a connection.
    /// Opens the system browser for the user to authenticate.
    /// </summary>
    public async Task<AuthResult> LoginAsync(string connectionId)
    {
        _logger.LogInformation("Login requested for connection: {ConnectionId}", connectionId);
        return await _tokenProvider.LoginInteractiveAsync(connectionId);
    }

    /// <summary>
    /// Signs out from the current connection.
    /// </summary>
    public async Task<bool> LogoutAsync()
    {
        _logger.LogInformation("Logout requested");
        return await _tokenProvider.LogoutAsync(null);
    }

    /// <summary>
    /// Gets the current authentication status.
    /// </summary>
    public async Task<AuthStatus> GetStatusAsync()
    {
        return await _tokenProvider.GetAuthStatusAsync(null);
    }

    /// <summary>
    /// Gets an access token for the active connection.
    /// Used by plugins via the token proxy mechanism.
    /// </summary>
    public async Task<AccessTokenResult> GetAccessTokenAsync(string? connectionId = null)
    {
        return await _tokenProvider.GetAccessTokenAsync(connectionId);
    }
}
