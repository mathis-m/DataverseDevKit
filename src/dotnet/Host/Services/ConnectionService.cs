using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages Dataverse connections with file-based persistence.
/// </summary>
public sealed class ConnectionService : IDisposable
{
    private readonly ILogger<ConnectionService> _logger;
    private readonly List<Connection> _connections = new();
    private readonly string _connectionsFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
        
        // Store connections in app local data folder
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataverseDevKit");
        Directory.CreateDirectory(appDataPath);
        _connectionsFilePath = Path.Combine(appDataPath, "connections.json");
    }

    /// <summary>
    /// Ensures connections are loaded from disk. Called lazily on first access.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;

            if (File.Exists(_connectionsFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_connectionsFilePath);
                    var data = JsonSerializer.Deserialize<ConnectionsData>(json, JsonOptions);
                    if (data?.Connections != null)
                    {
                        _connections.AddRange(data.Connections);
                        _logger.LogInformation("Loaded {Count} connections from disk", _connections.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load connections from disk, starting fresh");
                }
            }

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Persists connections to disk.
    /// </summary>
    private async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var data = new ConnectionsData { Connections = _connections.ToList() };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_connectionsFilePath, json);
            _logger.LogDebug("Saved {Count} connections to disk", _connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save connections to disk");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Connection>> ListConnectionsAsync()
    {
        await EnsureInitializedAsync();
        return _connections.ToList();
    }

    public async Task<Connection?> GetConnectionAsync(string id)
    {
        await EnsureInitializedAsync();
        return _connections.FirstOrDefault(c => c.Id == id);
    }

    public async Task<Connection?> GetActiveConnectionAsync()
    {
        await EnsureInitializedAsync();
        return _connections.FirstOrDefault(c => c.IsActive);
    }

    public async Task<Connection> SetActiveConnectionAsync(string id)
    {
        await EnsureInitializedAsync();
        
        var connection = _connections.FirstOrDefault(c => c.Id == id);
        if (connection == null)
        {
            throw new ArgumentException($"Connection not found: {id}");
        }

        foreach (var conn in _connections)
        {
            conn.IsActive = conn.Id == id;
        }

        _logger.LogInformation("Active connection set to: {Name}", connection.Name);
        await SaveAsync();

        return connection;
    }

    public async Task<Connection> AddConnectionAsync(AddConnectionParams params_)
    {
        await EnsureInitializedAsync();
        
        var connection = new Connection
        {
            Id = Guid.NewGuid().ToString(),
            Name = params_.Name,
            Url = params_.Url,
            IsActive = false,
            IsAuthenticated = false,
            AuthenticatedUser = null
        };

        _connections.Add(connection);
        _logger.LogInformation("Connection added: {Name}", connection.Name);
        await SaveAsync();

        return connection;
    }

    public async Task<bool> RemoveConnectionAsync(string id)
    {
        await EnsureInitializedAsync();
        
        var connection = _connections.FirstOrDefault(c => c.Id == id);
        if (connection == null)
        {
            return false;
        }

        _connections.Remove(connection);
        _logger.LogInformation("Connection removed: {Name}", connection.Name);
        await SaveAsync();

        return true;
    }

    /// <summary>
    /// Updates the authentication state for a connection.
    /// Called by TokenProviderService after login/logout.
    /// </summary>
    public async Task UpdateAuthStateAsync(string connectionId, bool isAuthenticated, string? user)
    {
        await EnsureInitializedAsync();
        
        var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection != null)
        {
            connection.IsAuthenticated = isAuthenticated;
            connection.AuthenticatedUser = user;
            _logger.LogInformation("Connection {Name} auth state updated: {IsAuth}, user: {User}", 
                connection.Name, isAuthenticated, user);
            await SaveAsync();
        }
    }

    /// <summary>
    /// Data structure for JSON persistence.
    /// </summary>
    private sealed class ConnectionsData
    {
        public List<Connection> Connections { get; set; } = new();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}

public record Connection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsActive { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? AuthenticatedUser { get; set; }
}

public record AddConnectionParams
{
    public required string Name { get; init; }
    public required string Url { get; init; }
}
