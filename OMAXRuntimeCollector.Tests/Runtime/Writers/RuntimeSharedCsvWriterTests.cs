using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Tests.Runtime.Writers;

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

        var lines = File.ReadAllLines(_testFilePath);
        Assert.Equal("MachineId,Date,MorningRuntime,AfternoonRuntime", lines[0]);
        Assert.Equal("OMAX-01,2026-09-08,03:31:11,00:00:00", lines[1]);
    }

    [Fact]
    public void SaveAfternoonRuntime_CreatesCsvWithAfternoonRuntime()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");

        writer.SaveAfternoonRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(2) + TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(32));

        var lines = File.ReadAllLines(_testFilePath);

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

        var lines = File.ReadAllLines(_testFilePath);
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

        var lines = File.ReadAllLines(_testFilePath);
        Assert.Single(lines.Skip(1));
        Assert.Equal("OMAX-01,2026-09-08,03:00:00,02:00:00", lines[1]);
    }

    [Fact]
    public void SaveMorningRuntime_DifferentDates_CreatesSeparateRows()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        
        writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3));
        writer.SaveMorningRuntime(new DateTime(2026, 9, 9), TimeSpan.FromHours(4));

        var lines = File.ReadAllLines(_testFilePath);
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

        var lines = File.ReadAllLines(_testFilePath);
        Assert.Equal(3, lines.Length);
        Assert.Contains("OMAX-01,2026-09-08,03:00:00,00:00:00", lines);
        Assert.Contains("OMAX-02,2026-09-08,04:00:00,00:00:00", lines);
    }
    
    [Fact]
    public async Task SaveMorningRuntime_WaitsForExistingLock()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        var lockFilePath = _testFilePath + ".lock";

        await using FileStream lockStream = new(
            lockFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        var writeTask = Task.Run(() =>
        {
            writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3));
        });

        // Give the writer enough time to encounter the lock.
        await Task.Delay(300);
        Assert.False(writeTask.IsCompleted);

        // Release the lock.
        await lockStream.DisposeAsync();
        await writeTask;

        Assert.True(File.Exists(_testFilePath));
        Assert.True(File.Exists(lockFilePath));
        var lines = await File.ReadAllLinesAsync(_testFilePath);
        Assert.Equal("OMAX-01,2026-09-08,03:00:00,00:00:00", lines[1]);
    }
    
    [Fact]
    public async Task TwoWriters_WritingSameCsv_PreserveBothMachines()
    {
        var writer1 = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");
        var writer2 = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-02");
        DateTime date = new(2026, 9, 8);

        var writeTask1 = Task.Run(() =>
        {
            writer1.SaveMorningRuntime(date, TimeSpan.FromHours(3));
        });

        var writeTask2 = Task.Run(() =>
        {
            writer2.SaveMorningRuntime(date, TimeSpan.FromHours(4));
        });

        await Task.WhenAll(writeTask1, writeTask2);

        var lines = await File.ReadAllLinesAsync(_testFilePath);
        Assert.Equal(3, lines.Length);
        Assert.Contains("OMAX-01,2026-09-08,03:00:00,00:00:00", lines);
        Assert.Contains("OMAX-02,2026-09-08,04:00:00,00:00:00", lines);
    }
    
    [Fact]
    public void SaveMorningRuntime_ThrowsWhenLockTimeoutIsReached()
    {
        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01", lockTimeoutMilliseconds: 500, lockRetryDelayMilliseconds: 50);
        var lockFilePath = _testFilePath + ".lock";

        using FileStream lockStream = new(
            lockFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.Throws<IOException>(() =>
        {
            writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3));
        });

        Assert.False(File.Exists(_testFilePath));
        Assert.True(File.Exists(lockFilePath));
    }
    
    [Fact]
    public void SaveMorningRuntime_HidesLockFile()
    {
        // The collector targets Windows in production, where the
        // Hidden file attribute is supported.
        if (!OperatingSystem.IsWindows()) return;

        var writer = new RuntimeSharedCsvWriter(_testFilePath, _logger, "OMAX-01");

        writer.SaveMorningRuntime(new DateTime(2026, 9, 8), TimeSpan.FromHours(3));

        var lockFilePath = _testFilePath + ".lock";
        Assert.True(File.Exists(lockFilePath));
        var attributes = File.GetAttributes(lockFilePath);
        Assert.True(attributes.HasFlag(FileAttributes.Hidden));
    }
}