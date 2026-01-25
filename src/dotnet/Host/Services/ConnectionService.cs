using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Manages Dataverse connections.
/// </summary>
public class ConnectionService
{
    private readonly ILogger<ConnectionService> _logger;
    private readonly List<Connection> _connections = new();
    private string? _activeConnectionId;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
        
        // Add a sample connection for development
        _connections.Add(new Connection
        {
            Id = "sample-dev",
            Name = "Development Environment",
            Url = "https://org.crm.dynamics.com",
            IsActive = true
        });
        _activeConnectionId = "sample-dev";
    }

    public Task<List<Connection>> ListConnectionsAsync()
    {
        return Task.FromResult(_connections.ToList());
    }

    public Task<Connection?> GetConnectionAsync(string id)
    {
        var connection = _connections.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(connection);
    }

    public Task<Connection> SetActiveConnectionAsync(string id)
    {
        var connection = _connections.FirstOrDefault(c => c.Id == id);
        if (connection == null)
        {
            throw new ArgumentException($"Connection not found: {id}");
        }

        foreach (var conn in _connections)
        {
            conn.IsActive = conn.Id == id;
        }

        _activeConnectionId = id;
        _logger.LogInformation("Active connection set to: {Name}", connection.Name);

        return Task.FromResult(connection);
    }

    public Task<Connection> AddConnectionAsync(AddConnectionParams params_)
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid().ToString(),
            Name = params_.Name,
            Url = params_.Url,
            IsActive = false
        };

        _connections.Add(connection);
        _logger.LogInformation("Connection added: {Name}", connection.Name);

        return Task.FromResult(connection);
    }

    public Task<bool> RemoveConnectionAsync(string id)
    {
        var connection = _connections.FirstOrDefault(c => c.Id == id);
        if (connection == null)
        {
            return Task.FromResult(false);
        }

        _connections.Remove(connection);
        _logger.LogInformation("Connection removed: {Name}", connection.Name);

        if (_activeConnectionId == id)
        {
            _activeConnectionId = null;
        }

        return Task.FromResult(true);
    }
}

public record Connection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public bool IsActive { get; set; }
}

public record AddConnectionParams
{
    public required string Name { get; init; }
    public required string Url { get; init; }
}
