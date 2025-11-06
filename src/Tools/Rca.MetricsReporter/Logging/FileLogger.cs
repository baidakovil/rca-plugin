namespace Rca.Tools.MetricsReporter.Logging;

using System;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Простейший файловый логгер для агрегатора метрик.
/// </summary>
public sealed class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Создаёт новый экземпляр <see cref="FileLogger"/>.
    /// </summary>
    /// <param name="logFilePath">Путь к лог-файлу.</param>
    public FileLogger(string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        var directory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    /// <summary>
    /// Записывает информационное сообщение.
    /// </summary>
    public void LogInformation(string message)
        => WriteLine("INFO", message);

    /// <summary>
    /// Записывает сообщение об ошибке.
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        var builder = new StringBuilder(message);
        if (exception is not null)
        {
            builder.Append(" :: ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
        }

        WriteLine("ERROR", builder.ToString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }

    private void WriteLine(string level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture);
        lock (_syncRoot)
        {
            _writer.WriteLine($"[{timestamp}] {level}: {message}");
        }
    }
}

