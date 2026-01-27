using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages the gRPC server that plugins call to get access tokens.
/// Uses Unix Domain Socket for secure local-only communication.
/// </summary>
public class TokenCallbackServer : IAsyncDisposable
{
    private readonly ILogger<TokenCallbackServer> _logger;
    private readonly TokenProviderService _tokenProvider;
    private readonly ConnectionService _connectionService;
    private readonly ILoggerFactory _loggerFactory;
    private WebApplication? _app;
    private string? _socketPath;
    private readonly object _lock = new();
    private bool _isStarted;

    public string? SocketPath => _socketPath;

    public TokenCallbackServer(
        ILogger<TokenCallbackServer> logger,
        TokenProviderService tokenProvider,
        ConnectionService connectionService,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _connectionService = connectionService;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Starts the token callback server if not already started.
    /// </summary>
    public async Task<string> StartAsync()
    {
        lock (_lock)
        {
            if (_isStarted && _socketPath != null)
            {
                return _socketPath;
            }
        }

        // Generate socket path
        var pid = Environment.ProcessId;
        _socketPath = OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), $"ddk-token-{pid}.sock")
            : $"/tmp/ddk/{pid}/token-callback.sock";

        // Ensure directory exists
        var socketDir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(socketDir) && !Directory.Exists(socketDir))
        {
            Directory.CreateDirectory(socketDir);
        }

        // Remove existing socket file
        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        _logger.LogInformation("Starting token callback server on: {SocketPath}", _socketPath);

        // Build the gRPC server
        var builder = WebApplication.CreateBuilder();
        
        // Configure logging to forward to the main app's logger
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ILoggerFactory>(_loggerFactory);

        builder.Services.AddGrpc();
        
        // Register services needed by TokenProviderGrpcService
        builder.Services.AddSingleton(_tokenProvider);
        builder.Services.AddSingleton(_connectionService);
        builder.Services.AddSingleton<TokenProviderGrpcService>();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenUnixSocket(_socketPath, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        _app = builder.Build();
        _app.MapGrpcService<TokenProviderGrpcService>();

        await _app.StartAsync();

        lock (_lock)
        {
            _isStarted = true;
        }

        _logger.LogInformation("Token callback server started");
        return _socketPath;
    }

    /// <summary>
    /// Stops the token callback server.
    /// </summary>
    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (!_isStarted)
            {
                return;
            }
            _isStarted = false;
        }

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        // Clean up socket file
        if (_socketPath != null && File.Exists(_socketPath))
        {
            try
            {
                File.Delete(_socketPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete socket file: {SocketPath}", _socketPath);
            }
        }

        _logger.LogInformation("Token callback server stopped");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
