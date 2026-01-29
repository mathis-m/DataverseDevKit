namespace DataverseDevKit.Core.Exceptions;

/// <summary>
/// Exception thrown when a user's authentication session has expired 
/// and reauthentication is required.
/// </summary>
public class SessionExpiredException : Exception
{
    /// <summary>
    /// The connection ID that requires reauthentication.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// The friendly name of the connection.
    /// </summary>
    public string? ConnectionName { get; }

    public SessionExpiredException()
        : base("Session expired. Please login again.")
    {
        ConnectionId = string.Empty;
    }

    public SessionExpiredException(string message)
        : base(message)
    {
        ConnectionId = string.Empty;
    }

    public SessionExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
        ConnectionId = string.Empty;
    }

    public SessionExpiredException(string connectionId, string? connectionName = null)
        : base($"Session expired for connection '{connectionName ?? connectionId}'. Please login again.")
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
    }

    public SessionExpiredException(string connectionId, string? connectionName, Exception innerException)
        : base($"Session expired for connection '{connectionName ?? connectionId}'. Please login again.", innerException)
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
    }
}
