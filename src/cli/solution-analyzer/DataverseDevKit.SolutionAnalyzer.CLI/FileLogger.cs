using Microsoft.Extensions.Logging;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Simple file logger for CLI output
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLogLevel;
    private readonly object _lock = new();

    public FileLogger(string logFilePath, LogLevel minLogLevel = LogLevel.Information)
    {
        _logFilePath = logFilePath;
        _minLogLevel = minLogLevel;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Clear or create log file
        File.WriteAllText(logFilePath, $"=== CLI Log Started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC ==={Environment.NewLine}");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLogLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{logLevel}] {message}";
        
        if (exception != null)
        {
            logEntry += $"{Environment.NewLine}{exception}";
        }

        lock (_lock)
        {
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
    }
}

/// <summary>
/// Simple logger factory for file logging
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLogLevel;

    public FileLoggerProvider(string logFilePath, LogLevel minLogLevel = LogLevel.Information)
    {
        _logFilePath = logFilePath;
        _minLogLevel = minLogLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_logFilePath, _minLogLevel);
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
