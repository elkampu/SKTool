using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SKTool.CCTVProtocols.Logging;

public sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly long _maxBytes;
    private readonly int _rollCount;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, JsonFileLogger> _loggers = new();

    public JsonFileLoggerProvider(string filePath, long maxBytes = 5 * 1024 * 1024, int rollCount = 5)
    {
        _filePath = filePath;
        _maxBytes = maxBytes;
        _rollCount = rollCount;

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, n => new JsonFileLogger(n, _filePath, _maxBytes, _rollCount, _lock));

    public void Dispose() => _loggers.Clear();
}

internal sealed class JsonFileLogger : ILogger
{
    private readonly string _category;
    private readonly string _filePath;
    private readonly long _maxBytes;
    private readonly int _rollCount;
    private readonly object _lock;

    public JsonFileLogger(string category, string filePath, long maxBytes, int rollCount, object sync)
    {
        _category = category;
        _filePath = filePath;
        _maxBytes = maxBytes;
        _rollCount = rollCount;
        _lock = sync;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);

        var now = DateTimeOffset.UtcNow;
        var json = new StringBuilder(512);
        json.Append('{');
        json.Append($"\"ts\":\"{now:O}\",");
        json.Append($"\"level\":\"{logLevel}\",");
        json.Append($"\"cat\":\"{Escape(_category)}\",");
        json.Append($"\"msg\":\"{Escape(message)}\"");
        if (exception is not null)
        {
            json.Append($",\"ex\":\"{Escape(exception.GetType().Name)}\"");
            json.Append($",\"exmsg\":\"{Escape(exception.Message)}\"");
            json.Append($",\"stack\":\"{Escape(exception.StackTrace ?? string.Empty)}\"");
        }
        json.Append("}\n");

        var bytes = Encoding.UTF8.GetBytes(json.ToString());

        lock (_lock)
        {
            RollIfNeeded_NoLock();
            File.AppendAllText(_filePath, Encoding.UTF8.GetString(bytes));
        }
    }

    private void RollIfNeeded_NoLock()
    {
        try
        {
            var fi = new FileInfo(_filePath);
            if (fi.Exists && fi.Length >= _maxBytes)
            {
                // roll old -> .N, current -> .1
                for (int i = _rollCount - 1; i >= 1; i--)
                {
                    var src = _filePath + "." + i;
                    var dst = _filePath + "." + (i + 1);
                    if (File.Exists(dst)) File.Delete(dst);
                    if (File.Exists(src)) File.Move(src, dst);
                }
                var first = _filePath + ".1";
                if (File.Exists(first)) File.Delete(first);
                if (File.Exists(_filePath)) File.Move(_filePath, first);
            }
        }
        catch
        {
            // best-effort logging; ignore roll errors
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}