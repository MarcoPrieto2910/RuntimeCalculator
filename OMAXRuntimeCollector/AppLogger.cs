namespace OMAXRuntimeCollector;

using System.Globalization;


/// <summary>
/// Provides application logging to a log file and, when test mode is enabled,
/// to the console.
/// </summary>
public class AppLogger
{
    private readonly string _logPath;
    private readonly bool _testMode;
    
    // Ensures that multiple threads cannot write to the log file simultaneously.
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLogger"/> class.
    /// </summary>
    /// <param name="logPath">
    /// The path of the log file. Environment variables in the path are expanded
    /// before the file is accessed.
    /// </param>
    /// <param name="testMode">
    /// Whether log messages should also be written to the console.
    /// </param>
    public AppLogger(string logPath,  bool testMode = true)
    {
        _logPath = ExpandPath(logPath);
        _testMode = testMode;

        string? directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
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
    /// Writes a formatted log message to the log file and, when test mode is
    /// enabled, to the console.
    /// </summary>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="message">The message to log.</param>
    private void Write(string level, string message)
    {
        string line =
            $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture)} " +
            $"[{level}] {message}";

        lock (_lock)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }

        if (_testMode)
            Console.WriteLine(line);
    }

    /// <summary>
    /// Expands environment variables contained in a file path.
    /// </summary>
    /// <param name="path">The path that may contain environment variables.</param>
    /// <returns>The path with any environment variables expanded.</returns>
    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}