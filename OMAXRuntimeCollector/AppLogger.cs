using Serilog;
using Serilog.Core;

namespace OMAXRuntimeCollector;


/// <summary>
/// Provides application logging to rolling log files and, when test mode is enabled,
/// to the console.
/// </summary>
public class AppLogger : IDisposable
{
    private readonly Logger _logger;
    private readonly bool _testMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLogger"/> class.
    /// </summary>
    /// <param name="logPath">
    /// The base path of the log file. Environment variables in the path are
    /// expanded before the logger is configured.
    /// </param>
    /// <param name="testMode">
    /// Whether log messages should also be written to the console.
    /// </param>
    public AppLogger(string logPath,  bool testMode = true)
    {
        string expandedPath = Environment.ExpandEnvironmentVariables(logPath);
        _testMode = testMode;

        string? directory = Path.GetDirectoryName(expandedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: expandedPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 30,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}")
            .CreateLogger();
    }


    /// <summary>
    /// Writes an informational message to the log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Info(string message)
    {
        Write("INFO", message);
    }

    /// <summary>
    /// Writes a warning message to the log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Warning(string message)
    {
        Write("WARNING", message);
    }

    /// <summary>
    /// Writes an error message to the log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Error(string message)
    {
        Write("ERROR", message);
    }


    /// <summary>
    /// Writes a message to Serilog and, when test mode is enabled, to the console.
    /// </summary>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="message">The message to log.</param>
    private void Write(string level, string message)
    {
        switch (level)
        {
            case "INFO":
                _logger.Information(message);
                break;

            case "WARNING":
                _logger.Warning(message);
                break;

            case "ERROR":
                _logger.Error(message);
                break;
        }

        if (_testMode)
        {
            string consoleLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            Console.WriteLine(consoleLine);
        }
    }
    
    
    /// <summary>
    /// Releases resources used by the logger.
    /// </summary>
    public void Dispose()
    {
        _logger.Dispose();
    }
}