using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OrbitStudio.Diagnostics;

/// <summary>
/// Schreibt Logeinträge in eine Tagesdatei (orbit-JJJJMMTT.log). Keine Netzwerkübertragung, keine Telemetrie.
/// Aufrufer sind dafür verantwortlich, keine Bildinhalte oder privaten Dateiinhalte zu loggen.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly int _retentionDays;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();
    private bool _disposed;

    public FileLoggerProvider(string directory, LogLevel minLevel = LogLevel.Information, int retentionDays = 14)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _minLevel = minLevel;
        _retentionDays = retentionDays;
        Directory.CreateDirectory(_directory);
        DeleteOldLogs();
    }

    public string CurrentLogFile => Path.Combine(_directory, $"orbit-{DateTime.Now:yyyyMMdd}.log");

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    public void Dispose() => _disposed = true;

    internal bool IsEnabled(LogLevel level) => !_disposed && level != LogLevel.None && level >= _minLevel;

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(" [").Append(ShortLevel(level)).Append("] ")
            .Append(category).Append(": ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();

        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(CurrentLogFile, line.ToString(), Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging darf die Anwendung nie zum Absturz bringen.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void DeleteOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "orbit-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
