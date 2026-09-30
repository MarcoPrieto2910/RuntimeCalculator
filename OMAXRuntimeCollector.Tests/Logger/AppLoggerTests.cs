namespace OMAXRuntimeCollector.Tests.Logger;

[Collection("Console Tests")]
public class AppLoggerTests : IDisposable
{
    private readonly string _testDirectory;

    public AppLoggerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    [Fact]
    public void Info_WritesMessageToLogFile()
    {
        var logPath = Path.Combine(_testDirectory, "collector.log");
        using (var logger = new AppLogger(logPath, testMode: false))
        {
            logger.Info("Test log message.");
        }

        var logFiles = Directory.GetFiles(_testDirectory, "collector*.log");
        Assert.Single(logFiles);
        var contents = File.ReadAllText(logFiles[0]);
        Assert.Contains("Information", contents);
        Assert.Contains("Test log message.", contents);
    }

    [Fact]
    public void Info_WritesMessageToConsole_WhenTestModeEnabled()
    {
        var logPath = Path.Combine(_testDirectory, "collector.log");
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            using (var logger = new AppLogger(logPath, testMode: true))
            {
                logger.Info("Test console message.");
            }

            var consoleOutput = output.ToString();
            Assert.Contains("[INFO] Test console message.", consoleOutput);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public void Info_CreatesNewLogFile_WhenFileSizeLimitIsExceeded()
    {
        var logPath = Path.Combine(_testDirectory, "collector.log");
        using (var logger = new AppLogger(logPath, testMode: false, fileSizeLimitBytes: 1024))
        {
            var message = new string('A', 500);

            logger.Info(message);
            logger.Info(message);
            logger.Info(message);
        }

        var logFiles = Directory.GetFiles(_testDirectory, "collector*.log");
        Assert.True(logFiles.Length > 1);
    }

    [Fact]
    public void Info_DoesNotWriteToConsole_WhenTestModeDisabled()
    {
        var logPath = Path.Combine(_testDirectory);
        using var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            using (var logger = new AppLogger(logPath, testMode: false))
            {
                logger.Info("Test console message.");
            }

            Assert.Empty(output.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }
}