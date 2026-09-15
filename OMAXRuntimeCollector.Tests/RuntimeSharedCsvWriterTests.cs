using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Tests;

public class RuntimeSharedCsvWriterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly AppLogger _logger;

    public RuntimeSharedCsvWriterTests()
    {
        // -----------------------------------------------------
        // Create a unique temporary directory for this test.
        // -----------------------------------------------------
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "runtime.csv");
        var logPath = Path.Combine(_testDirectory, "test.log");
        _logger = new AppLogger(logPath);
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


    [Fact]
    public void SaveMorningRuntime_CreatesCsvWithMorningRuntime()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        
        writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3) + TimeSpan.FromMinutes(31) + TimeSpan.FromSeconds(11));

        string[] lines = File.ReadAllLines(_testFilePath);
        Assert.Equal("MachineId,Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("OMAX-01,2026-09-08,03:31:11,00:00:00", lines[1]);
    }

    [Fact]
    public void SaveAfternoonRuntime_CreatesCsvWithAfternoonRuntime()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");

        writer.SaveAfternoonRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(2) + TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(32));

        string[] lines = File.ReadAllLines(_testFilePath);

        Assert.Equal("MachineId,Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("OMAX-01,2026-09-08,00:00:00,02:14:32", lines[1]);
    }

    [Fact]
    public void SaveAfternoonRuntime_UpdatesExistingMorningRow()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        DateTime date = new(2026, 9, 8);

        writer.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        writer.SaveAfternoonRuntime(date, TimeSpan.FromHours(2));

        string[] lines = File.ReadAllLines(_testFilePath);
        Assert.Single(lines.Skip(1));
        Assert.Equal("OMAX-01,2026-09-08,03:00:00,02:00:00", lines[1]);
    }

    [Fact]
    public void SaveMorningRuntime_UpdatesExistingAfternoonRow()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        DateTime date = new(2026, 9, 8);

        writer.SaveAfternoonRuntime(date, TimeSpan.FromHours(2));
        writer.SaveMorningRuntime(date, TimeSpan.FromHours(3));

        string[] lines = File.ReadAllLines(_testFilePath);
        Assert.Single(lines.Skip(1));
        Assert.Equal("OMAX-01,2026-09-08,03:00:00,02:00:00", lines[1]);
    }

    [Fact]
    public void SaveMorningRuntime_DifferentDates_CreatesSeparateRows()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        
        writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3));
        writer.SaveMorningRuntime(new DateTime(2026, 9, 9), TimeSpan.FromHours(4));

        string[] lines = File.ReadAllLines(_testFilePath);
        Assert.Equal(3, lines.Length);
        Assert.Contains("OMAX-01,2026-09-08,03:00:00,00:00:00", lines);
        Assert.Contains("OMAX-01,2026-09-09,04:00:00,00:00:00", lines);
    }

    [Fact]
    public void SaveMorningRuntime_DifferentMachines_CreatesSeparateRows()
    {
        var writer1 = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        var writer2 = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-02");
        DateTime date = new(2026, 9, 8);

        writer1.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        writer2.SaveMorningRuntime(date, TimeSpan.FromHours(4));

        string[] lines = File.ReadAllLines(_testFilePath);
        Assert.Equal(3, lines.Length);
        Assert.Contains("OMAX-01,2026-09-08,03:00:00,00:00:00", lines);
        Assert.Contains("OMAX-02,2026-09-08,04:00:00,00:00:00", lines);
    }
}