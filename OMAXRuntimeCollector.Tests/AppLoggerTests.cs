namespace OMAXRuntimeCollector.Tests;

public class AppLoggerTests: IDisposable
{
    private readonly string _testDirectory;
    private readonly TextWriter _originalOutput;

    public AppLoggerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _originalOutput = Console.Out;
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
    
    
    [Fact]
    public void Info_WritesMessageToLogFile()
    {
        string logPath = Path.Combine(_testDirectory, "collector.log");
        using (var logger = new AppLogger(logPath, testMode: false))
        {
            logger.Info("Test log message.");
        }

        string[] logFiles = Directory.GetFiles(_testDirectory, "collector*.log");
        
        Assert.Single(logFiles);
        string contents = File.ReadAllText(logFiles[0]);
        Assert.Contains("Information", contents);
        Assert.Contains("Test log message.", contents);
    }
    
    [Fact]
    public void Info_WritesMessageToConsole_WhenTestModeEnabled()
    {
        string logPath = Path.Combine(_testDirectory, "collector.log");
        using var output = new StringWriter();
        Console.SetOut(output);

        using (var logger = new AppLogger(logPath, testMode: true))
        {
            logger.Info("Test console message.");
        }
        
        string consoleOutput = output.ToString();
        Assert.Contains("[INFO] Test console message.", consoleOutput);
    }
    
    [Fact]
    public void Info_CreatesNewLogFile_WhenFileSizeLimitIsExceeded()
    {
        string logPath = Path.Combine(_testDirectory, "collector.log");
        using (var logger = new AppLogger(logPath, testMode: false, fileSizeLimitBytes: 1024))
        {
            string message = new string('A', 500);

            logger.Info(message);
            logger.Info(message);
            logger.Info(message);
        }

        string[] logFiles = Directory.GetFiles(_testDirectory, "collector*.log");
        Assert.True(logFiles.Length > 1);
    }
}