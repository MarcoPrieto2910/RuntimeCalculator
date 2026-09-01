namespace OMAXRuntimeCollector;

using System.Globalization;

public class AppLogger
{
    private readonly string _logPath;
    private readonly bool _testMode;
    private readonly object _lock = new();

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


    public void Info(string message)
    {
        Write("INFO", message);
    }


    public void Warning(string message)
    {
        Write("WARNING", message);
    }


    public void Error(string message)
    {
        Write("ERROR", message);
    }


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


    private static string ExpandPath(string path)
    {
        return Environment.ExpandEnvironmentVariables(path);
    }
}