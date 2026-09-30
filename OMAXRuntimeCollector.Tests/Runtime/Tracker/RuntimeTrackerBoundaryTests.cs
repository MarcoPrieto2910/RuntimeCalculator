using OMAXRuntimeCollector.Runtime;
using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Tests.Runtime.Tracker;

public class RuntimeTrackerBoundaryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _csvPath;
    private readonly string _logPath;
    private readonly AppLogger _logger;


    public RuntimeTrackerBoundaryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _csvPath = Path.Combine(_testDirectory, "runtime.csv");
        _logPath = Path.Combine(_testDirectory, "collector.log");
        _logger = new AppLogger(_logPath);
    }
    
    
    // =========================================================
    // HELPER
    // =========================================================
    private RuntimeTracker CreateTracker()
    {
        var writer = new RuntimeLocalCsvWriter(_csvPath, _logger, "OMAX-01");
        var calculator = new RuntimeCalculator();

        return new RuntimeTracker([writer], _logger, calculator);
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


    // =========================================================
    // HELPER
    // =========================================================

    private static string LocalTimestamp(DateTime localTime)
    {
        DateTimeOffset localDateTime =
            new DateTimeOffset(
                localTime,
                TimeZoneInfo.Local.GetUtcOffset(localTime));

        return localDateTime
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ");
    }


    // =========================================================
    // 14:00
    // =========================================================

    [Fact]
    public void ProcessTimeBoundary_At14_SavesMorningRuntime()
    {
        var tracker = CreateTracker();

        // -----------------------------------------------------
        // Simulate:
        //
        // 10:00 → 12:00 local time
        //
        // = 2 hours of morning runtime.
        // -----------------------------------------------------

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 10, 0, 0))}" + "|mode|AUTOMATIC|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 12, 0, 0))}" + "|execution|STOPPED");


        // -----------------------------------------------------
        // Simulate the 14:00 boundary.
        // -----------------------------------------------------

        tracker.ProcessTimeBoundary(
            new DateTime(
                2026,
                8,
                25,
                14,
                0,
                0));


        Assert.True(File.Exists(_csvPath));
        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,02:00:00,00:00:00", lines[1]);
    }


    // =========================================================
    // EXECUTION CROSSING 14:00
    // =========================================================

    [Fact]
    public void ProcessTimeBoundary_At14_SplitsActiveExecution()
    {
        var tracker = CreateTracker();

        // -----------------------------------------------------
        // Machine starts at 13:30 local time.
        // -----------------------------------------------------
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 13, 30, 0))}" + "|mode|AUTOMATIC|execution|ACTIVE");


        // -----------------------------------------------------
        // 14:00 boundary.
        // -----------------------------------------------------

        tracker.ProcessTimeBoundary(
            new DateTime(
                2026,
                8,
                25,
                14,
                0,
                0));


        // -----------------------------------------------------
        // 13:30 → 14:00
        //
        // = 30 minutes morning.
        // -----------------------------------------------------

        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal("OMAX-01,2026-08-25,00:30:00,00:00:00", lines[1]);


        // -----------------------------------------------------
        // Machine stops at 14:30.
        //
        // RuntimeTracker moved executionStart to 14:00,
        // therefore:
        //
        // 14:00 → 14:30
        //
        // = 30 minutes afternoon.
        // -----------------------------------------------------

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 14, 30, 0))}" + "|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();
        Assert.Equal(TimeSpan.FromMinutes(30), runtime.Morning);
        Assert.Equal(TimeSpan.FromMinutes(30), runtime.Afternoon);
    }


    // =========================================================
    // MIDNIGHT
    // =========================================================

    [Fact]
    public void ProcessTimeBoundary_AtMidnight_SavesAfternoonRuntime()
    {
        var tracker = CreateTracker();


        // -----------------------------------------------------
        // Simulate:
        //
        // 15:00 → 16:00 local time
        //
        // = 1 hour afternoon runtime.
        // -----------------------------------------------------

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 15, 0, 0))}" + "|mode|AUTOMATIC|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 16, 0, 0))}" + "|execution|STOPPED");


        // -----------------------------------------------------
        // Simulate midnight.
        // -----------------------------------------------------

        tracker.ProcessTimeBoundary(
            new DateTime(
                2026,
                8,
                26,
                0,
                0,
                0));


        // -----------------------------------------------------
        // Previous day's row should contain:
        //
        // 2026-08-25
        // Morning   = 00:00:00
        // Afternoon = 01:00:00
        // -----------------------------------------------------

        var lines = File.ReadAllLines(_csvPath);
        
        Assert.Equal(2, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,00:00:00,01:00:00", lines[1]);
    }


    // =========================================================
    // ACTIVE EXECUTION THROUGH MIDNIGHT
    // =========================================================

    [Fact]
    public void ProcessTimeBoundary_AtMidnight_SplitsActiveExecution()
    {
        var tracker = CreateTracker();


        // -----------------------------------------------------
        // Machine starts at 23:30 local time.
        // -----------------------------------------------------

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 23, 30, 0))}" + "|mode|AUTOMATIC|execution|ACTIVE");


        // -----------------------------------------------------
        // Midnight boundary.
        // -----------------------------------------------------

        tracker.ProcessTimeBoundary(
            new DateTime(
                2026,
                8,
                26,
                0,
                0,
                0));


        // -----------------------------------------------------
        // 23:30 → 00:00
        //
        // = 30 minutes afternoon.
        // -----------------------------------------------------

        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal("OMAX-01,2026-08-25,00:00:00,00:30:00", lines[1]);


        // -----------------------------------------------------
        // Machine stops at 00:30.
        //
        // 00:00 → 00:30 is outside our accounting periods.
        // Therefore, nothing should be added.
        // -----------------------------------------------------
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 26, 0, 30, 0))}" + "|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();
        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }
    
    [Fact]
    public void ProcessTimeBoundary_AtMidnight_ContinuesActiveExecutionIntoNewDay()
    {
        var tracker = CreateTracker();

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 23, 30, 0))}" + "|execution|ACTIVE");
        tracker.ProcessTimeBoundary(new DateTime(2026, 8, 26, 0, 0, 0));
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 26, 2, 0, 0))}" + "|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();

        // The 23:30 → 00:00 portion belongs to the previous day.
        // The 00:00 → 02:00 portion is outside our accounting periods.
        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal("OMAX-01,2026-08-25,00:00:00,00:30:00", lines[1]);
    }
    
    [Fact]
    public void ProcessTimeBoundary_AtMidnight_StartsNewRuntimeDay()
    {
        var tracker = CreateTracker();

        // Day 1 afternoon.
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 15, 0, 0))}" + "|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 16, 0, 0))}" + "|execution|STOPPED");

        // Midnight.
        tracker.ProcessTimeBoundary(new DateTime(2026, 8, 26, 0, 0, 0));

        // Day 2 morning.
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 26, 10, 0, 0))}" + "|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 26, 11, 0, 0))}" + "|execution|STOPPED");

        // 14:00 saves Day 2 morning runtime.
        tracker.ProcessTimeBoundary(new DateTime(2026, 8, 26, 14, 0, 0));

        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal(3, lines.Length);
        Assert.Equal("OMAX-01,2026-08-25,00:00:00,01:00:00", lines[1]);
        Assert.Equal("OMAX-01,2026-08-26,01:00:00,00:00:00", lines[2]);
    }
    
    [Fact]
    public void ProcessTimeBoundary_ExecutionEndsExactlyAt14_IsMorningRuntime()
    {
        var tracker = CreateTracker();

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 13, 0, 0))}" + "|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 14, 0, 0))}" + "|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();
        Assert.Equal(TimeSpan.FromHours(1), runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }
    
    [Fact]
    public void ExecutionStartsExactlyAt14_IsAfternoonRuntime()
    {
        var tracker = CreateTracker();

        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 14, 0, 0))}" + "|execution|ACTIVE");
        tracker.ProcessLine($"{LocalTimestamp(new DateTime(2026, 8, 25, 15, 0, 0))}" + "|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();
        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.FromHours(1), runtime.Afternoon);
    }
}