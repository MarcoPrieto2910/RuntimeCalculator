using OMAXRuntimeCollector.Runtime;
using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Tests;

using OMAXRuntimeCollector;
using Xunit;

public class RuntimeLocalCsvWriterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _csvPath;
    private readonly AppLogger _logger;

    public RuntimeLocalCsvWriterTests()
    {
        // -----------------------------------------------------
        // Create a unique temporary directory for this test.
        // -----------------------------------------------------
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _csvPath = Path.Combine(_testDirectory, "runtime.csv");
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


    // =========================================================
    // CREATE CSV
    // =========================================================

    [Fact]
    public void SaveMorningRuntime_CreatesCsvFile()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01"); 
        DateTime date = new DateTime(2026, 8, 25);
        TimeSpan morningRuntime = TimeSpan.FromHours(3);

        writer.SaveMorningRuntime(date, morningRuntime);
        
        Assert.True(File.Exists(_csvPath));
        string[] lines = File.ReadAllLines(_csvPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("MachineId,Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("OMAX-01,2026-08-25,03:00:00,00:00:00", lines[1]);
    }


    // =========================================================
    // UPDATE EXISTING ROW
    // =========================================================

    [Fact]
    public void SaveAfternoonRuntime_UpdatesExistingRow()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
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
        Assert.Equal("MachineId,Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("OMAX-01,2026-08-25,03:00:00,02:00:00", lines[1]);
    }


    // =========================================================
    // MORNING VALUE IS PRESERVED
    // =========================================================

    [Fact]
    public void SaveAfternoonRuntime_DoesNotOverwriteMorningRuntime()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        DateTime date = new DateTime(2026, 8, 25);
        
        writer.SaveMorningRuntime(date, TimeSpan.FromMinutes(90));
        writer.SaveAfternoonRuntime(date, TimeSpan.FromMinutes(45));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal("OMAX-01,2026-08-25,01:30:00,00:45:00", lines[1]);
    }


    // =========================================================
    // MULTIPLE DAYS
    // =========================================================

    [Fact]
    public void SaveRuntime_ForMultipleDays_CreatesSeparateRows()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        DateTime day1 = new DateTime(2026, 8, 25);
        DateTime day2 = new DateTime(2026, 8, 26);


        writer.SaveMorningRuntime(day1, TimeSpan.FromHours(2));
        writer.SaveMorningRuntime(day2, TimeSpan.FromHours(4));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal(3, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,02:00:00,00:00:00", lines[1]);
        Assert.Equal("OMAX-01,2026-08-26,04:00:00,00:00:00", lines[2]);
    }


    // =========================================================
    // SAME DAY IS NOT DUPLICATED
    // =========================================================

    [Fact]
    public void SaveMorningRuntime_TwiceForSameDay_UpdatesExistingRow()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        DateTime date = new DateTime(2026, 8, 25);


        writer.SaveMorningRuntime(date, TimeSpan.FromHours(2));
        writer.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        string[] lines = File.ReadAllLines(_csvPath);


        Assert.Equal(2, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,03:00:00,00:00:00", lines[1]);
    }
    
    // =========================================================
    // MACHINE ID IS PART OF ROW LOOKUP
    // =========================================================

    [Fact]
    public void SaveMorningRuntime_UpdatesCorrectMachineRow()
    {
        DateTime date = new DateTime(2026, 8, 25);

        // -----------------------------------------------------
        // Create two writers representing two machines.
        // -----------------------------------------------------
        var writer01 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        var writer02 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-02");


        // -----------------------------------------------------
        // Create a row for each machine on the same date.
        // -----------------------------------------------------
        writer01.SaveMorningRuntime(date, TimeSpan.FromHours(2));
        writer02.SaveMorningRuntime(date, TimeSpan.FromHours(4));


        // -----------------------------------------------------
        // Update OMAX-01 only.
        // -----------------------------------------------------
        writer01.SaveMorningRuntime(date, TimeSpan.FromHours(3));


        // -----------------------------------------------------
        // Both machines should still have their own row.
        // -----------------------------------------------------
        string[] lines = File.ReadAllLines(_csvPath);

        Assert.Equal(3, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,03:00:00,00:00:00", lines[1]);
        Assert.Equal("OMAX-02,2026-08-25,04:00:00,00:00:00", lines[2]);
    }
    
    [Fact]
    public void SaveAfternoonRuntime_UpdatesCorrectMachineRow()
    {
        DateTime date = new DateTime(2026, 8, 25);
        var writer01 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        var writer02 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-02");


        writer01.SaveMorningRuntime(date, TimeSpan.FromHours(2));
        writer02.SaveMorningRuntime(date, TimeSpan.FromHours(4));
        
        writer01.SaveAfternoonRuntime(date, TimeSpan.FromHours(1));


        string[] lines = File.ReadAllLines(_csvPath);
        
        Assert.Equal(3, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,02:00:00,01:00:00", lines[1]);
        Assert.Equal("OMAX-02,2026-08-25,04:00:00,00:00:00", lines[2]);
    }
    
    [Fact]
    public void SaveRuntime_ForMultipleMachinesAndDays_UpdatesCorrectRow()
    {
        DateTime day1 = new DateTime(2026, 8, 25);
        DateTime day2 = new DateTime(2026, 8, 26);

        var writer01 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        var writer02 = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-02");


        // -----------------------------------------------------
        // Create rows for both machines on both days.
        // -----------------------------------------------------
        writer01.SaveMorningRuntime(day1, TimeSpan.FromHours(2));
        writer02.SaveMorningRuntime(day1, TimeSpan.FromHours(4));

        writer01.SaveMorningRuntime(day2, TimeSpan.FromHours(3));
        writer02.SaveMorningRuntime(day2, TimeSpan.FromHours(5));


        // -----------------------------------------------------
        // Update only OMAX-01 on day 2.
        // -----------------------------------------------------
        writer01.SaveMorningRuntime(day2, TimeSpan.FromHours(6));


        string[] lines = File.ReadAllLines(_csvPath);
        
        Assert.Equal(5, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,02:00:00,00:00:00", lines[1]);
        Assert.Equal("OMAX-02,2026-08-25,04:00:00,00:00:00", lines[2]);
        Assert.Equal("OMAX-01,2026-08-26,06:00:00,00:00:00", lines[3]);
        Assert.Equal("OMAX-02,2026-08-26,05:00:00,00:00:00", lines[4]);
    }
}