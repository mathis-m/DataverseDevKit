using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages authentication and token storage.
/// </summary>
public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private bool _isAuthenticated;
    private string? _currentUser;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }

    public Task<AuthResult> LoginAsync(string connectionId)
    {
        _logger.LogInformation("Login requested for connection: {ConnectionId}", connectionId);
        
        // TODO: Implement actual OAuth/MSAL authentication
        // For now, return a stub response
        _isAuthenticated = true;
        _currentUser = "developer@contoso.com";

        return Task.FromResult(new AuthResult
        {
            Success = true,
            User = _currentUser
        });
    }

    public Task<bool> LogoutAsync()
    {
        _logger.LogInformation("Logout requested");
        
        _isAuthenticated = false;
        _currentUser = null;

        return Task.FromResult(true);
    }

    public Task<AuthStatus> GetStatusAsync()
    {
        return Task.FromResult(new AuthStatus
        {
            IsAuthenticated = _isAuthenticated,
            User = _currentUser
        });
    }
}

public record AuthResult
{
    public bool Success { get; init; }
    public string? User { get; init; }
    public string? Error { get; init; }
}

public record AuthStatus
{
    public bool IsAuthenticated { get; init; }
    public string? User { get; init; }
}
