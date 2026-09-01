using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.Tests;

using OMAXRuntimeCollector;
using Xunit;

public class RuntimeCsvWriterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _csvPath;
    private readonly string _logPath;
    private readonly AppLogger _logger;

    public RuntimeCsvWriterTests()
    {
        // -----------------------------------------------------
        // Create a unique temporary directory for this test.
        // -----------------------------------------------------
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _csvPath = Path.Combine(_testDirectory, "runtime.csv");
        _logPath = Path.Combine(_testDirectory, "test.log");
        _logger = new AppLogger(_logPath);
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    public void Dispose()
    {
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


    // =========================================================
    // CREATE CSV
    // =========================================================

    [Fact]
    public void SaveMorningRuntime_CreatesCsvFile()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        DateTime date = new DateTime(2026, 8, 25);
        TimeSpan morningRuntime = TimeSpan.FromHours(3);

        writer.SaveMorningRuntime(date, morningRuntime);
        
        Assert.True(File.Exists(_csvPath));
        string[] lines = File.ReadAllLines(_csvPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("2026-08-25,03:00:00,00:00:00", lines[1]);
    }


    // =========================================================
    // UPDATE EXISTING ROW
    // =========================================================

    [Fact]
    public void SaveAfternoonRuntime_UpdatesExistingRow()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        DateTime date = new DateTime(2026, 8, 25);


        // -----------------------------------------------------
        // First save the morning runtime.
        // -----------------------------------------------------
        writer.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        
        // -----------------------------------------------------
        // Then save the afternoon runtime.
        // -----------------------------------------------------
        writer.SaveAfternoonRuntime(date, TimeSpan.FromHours(2));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal(2, lines.Length);
        Assert.Equal("Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("2026-08-25,03:00:00,02:00:00", lines[1]);
    }


    // =========================================================
    // MORNING VALUE IS PRESERVED
    // =========================================================

    [Fact]
    public void SaveAfternoonRuntime_DoesNotOverwriteMorningRuntime()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        DateTime date = new DateTime(2026, 8, 25);
        
        writer.SaveMorningRuntime(date, TimeSpan.FromMinutes(90));
        writer.SaveAfternoonRuntime(date, TimeSpan.FromMinutes(45));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal("2026-08-25,01:30:00,00:45:00", lines[1]);
    }


    // =========================================================
    // MULTIPLE DAYS
    // =========================================================

    [Fact]
    public void SaveRuntime_ForMultipleDays_CreatesSeparateRows()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        DateTime day1 = new DateTime(2026, 8, 25);
        DateTime day2 = new DateTime(2026, 8, 26);


        writer.SaveMorningRuntime(day1, TimeSpan.FromHours(2));
        writer.SaveMorningRuntime(day2, TimeSpan.FromHours(4));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal(3, lines.Length);
        Assert.Equal("2026-08-25,02:00:00,00:00:00", lines[1]);
        Assert.Equal("2026-08-26,04:00:00,00:00:00", lines[2]);
    }


    // =========================================================
    // SAME DAY IS NOT DUPLICATED
    // =========================================================

    [Fact]
    public void SaveMorningRuntime_TwiceForSameDay_UpdatesExistingRow()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        DateTime date = new DateTime(2026, 8, 25);


        writer.SaveMorningRuntime(date, TimeSpan.FromHours(2));
        writer.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal(2, lines.Length);
        Assert.Equal("2026-08-25,03:00:00,00:00:00", lines[1]);
    }
}