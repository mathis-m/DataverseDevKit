using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages access tokens for Dataverse connections using MSAL.
/// Provides tokens on-demand with automatic refresh.
/// </summary>
public sealed class TokenProviderService : IDisposable
{
    private readonly ILogger<TokenProviderService> _logger;
    private readonly TokenCacheService _tokenCacheService;
    private readonly ConnectionService _connectionService;
    
    // MSAL application - one per tenant would be more efficient, but for simplicity we use one
    private IPublicClientApplication? _msalApp;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    // Well-known Dataverse scope suffix
    private const string DataverseScopeSuffix = "/.default";
    
    // Microsoft's well-known client ID for developer tools (like Power Platform CLI)
    // This is a public client that supports interactive login
    private const string DefaultClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    
    // Redirect URI for desktop apps
    private const string RedirectUri = "http://localhost";

    public TokenProviderService(
        ILogger<TokenProviderService> logger,
        TokenCacheService tokenCacheService,
        ConnectionService connectionService)
    {
        _logger = logger;
        _tokenCacheService = tokenCacheService;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Ensures MSAL is initialized with token cache.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            _msalApp = PublicClientApplicationBuilder
                .Create(DefaultClientId)
                .WithRedirectUri(RedirectUri)
                .WithAuthority(AzureCloudInstance.AzurePublic, "common")
                .WithLogging((level, message, containsPii) =>
                {
                    if (!containsPii)
                    {
                        _logger.LogDebug("[MSAL] {Level}: {Message}", level, message);
                    }
                }, Microsoft.Identity.Client.LogLevel.Warning, enablePiiLogging: false, enableDefaultPlatformLogging: true)
                .Build();

            await _tokenCacheService.RegisterCacheAsync(_msalApp);
            _isInitialized = true;

            _logger.LogInformation("TokenProviderService initialized");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets an access token for the specified connection.
    /// Tries silent authentication first, then throws if not authenticated.
    /// </summary>
    /// <param name="connectionId">The connection ID, or null for active connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The access token.</returns>
    public async Task<string> GetAccessTokenAsync(string? connectionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var connection = await GetConnectionAsync(connectionId);
        var scopes = GetScopesForConnection(connection);

        try
        {
            // Try to get token silently (from cache or refresh)
            var accounts = await _msalApp!.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                throw new InvalidOperationException(
                    $"No authenticated account found for connection '{connection.Name}'. Please login first.");
            }

            var result = await _msalApp.AcquireTokenSilent(scopes, account)
                .ExecuteAsync(ct);

            _logger.LogDebug("Token acquired silently for {ConnectionName}, expires: {Expiry}",
                connection.Name, result.ExpiresOn);

            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogWarning("Silent token acquisition failed: {Message}", ex.Message);
            throw new InvalidOperationException(
                $"Session expired for connection '{connection.Name}'. Please login again.", ex);
        }
    }

    /// <summary>
    /// Initiates interactive OAuth login for a connection.
    /// Opens the system browser for the user to authenticate.
    /// </summary>
    public async Task<AuthResult> LoginInteractiveAsync(string? connectionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var connection = await GetConnectionAsync(connectionId);
        var scopes = GetScopesForConnection(connection);

        try
        {
            _logger.LogInformation("Starting interactive login for connection: {ConnectionName}", connection.Name);

            // Use system browser for OAuth flow
            var result = await _msalApp!.AcquireTokenInteractive(scopes)
                .WithUseEmbeddedWebView(false) // Use system browser
                .WithSystemWebViewOptions(new SystemWebViewOptions
                {
                    HtmlMessageSuccess = "<html><body><h1>Authentication Successful</h1><p>You can close this window and return to DataverseDevKit.</p></body></html>",
                    HtmlMessageError = "<html><body><h1>Authentication Failed</h1><p>Please close this window and try again.</p></body></html>",
                })
                .ExecuteAsync(ct);

            _logger.LogInformation("Interactive login successful for {User}", result.Account.Username);

            // Update connection auth state
            await _connectionService.UpdateAuthStateAsync(connection.Id, true, result.Account.Username);

            return new AuthResult
            {
                Success = true,
                User = result.Account.Username,
                ExpiresOn = result.ExpiresOn
            };
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "Interactive login failed for connection: {ConnectionName}", connection.Name);
            return new AuthResult
            {
                Success = false,
                Error = ex.Message
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Login cancelled by user");
            return new AuthResult
            {
                Success = false,
                Error = "Login cancelled by user"
            };
        }
    }

    /// <summary>
    /// Signs out from a connection, clearing cached tokens.
    /// </summary>
    public async Task<bool> LogoutAsync(string? connectionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var connection = await GetConnectionAsync(connectionId);

        try
        {
            var accounts = await _msalApp!.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _msalApp.RemoveAsync(account);
            }

            // Update connection auth state
            await _connectionService.UpdateAuthStateAsync(connection.Id, false, null);

            _logger.LogInformation("Logged out from connection: {ConnectionName}", connection.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout from connection: {ConnectionName}", connection.Name);
            return false;
        }
    }

    /// <summary>
    /// Checks if a connection has valid (or refreshable) tokens.
    /// </summary>
    public async Task<bool> HasValidTokenAsync(string? connectionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        try
        {
            var connection = await GetConnectionAsync(connectionId);
            var scopes = GetScopesForConnection(connection);

            var accounts = await _msalApp!.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                return false;
            }

            // Try silent acquisition - this will use cache or refresh token
            var result = await _msalApp.AcquireTokenSilent(scopes, account)
                .ExecuteAsync(ct);

            return result != null && !string.IsNullOrEmpty(result.AccessToken);
        }
        catch (MsalUiRequiredException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking token validity");
            return false;
        }
    }

    /// <summary>
    /// Gets authentication status for a connection.
    /// </summary>
    public async Task<AuthStatus> GetAuthStatusAsync(string? connectionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        try
        {
            var accounts = await _msalApp!.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                return new AuthStatus
                {
                    IsAuthenticated = false
                };
            }

            var hasValidToken = await HasValidTokenAsync(connectionId, ct);

            return new AuthStatus
            {
                IsAuthenticated = hasValidToken,
                User = account.Username
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting auth status");
            return new AuthStatus
            {
                IsAuthenticated = false
            };
        }
    }

    private async Task<Connection> GetConnectionAsync(string? connectionId)
    {
        Connection? connection;
        
        if (string.IsNullOrEmpty(connectionId))
        {
            var connections = await _connectionService.ListConnectionsAsync();
            connection = connections.FirstOrDefault(c => c.IsActive);
        }
        else
        {
            connection = await _connectionService.GetConnectionAsync(connectionId);
        }

        if (connection == null)
        {
            throw new InvalidOperationException(
                connectionId == null
                    ? "No active connection found"
                    : $"Connection not found: {connectionId}");
        }

        return connection;
    }

    private static string[] GetScopesForConnection(Connection connection)
    {
        // Build the scope from the connection URL
        // e.g., https://org.crm.dynamics.com -> https://org.crm.dynamics.com/.default
        var baseUrl = connection.Url.TrimEnd('/');
        return new[] { $"{baseUrl}/.default" };
    }

    public void Dispose()
    {
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public record AuthResult
{
    public bool Success { get; init; }
    public string? User { get; init; }
    public DateTimeOffset? ExpiresOn { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Current authentication status.
/// </summary>
public record AuthStatus
{
    public bool IsAuthenticated { get; init; }
    public string? User { get; init; }
}
