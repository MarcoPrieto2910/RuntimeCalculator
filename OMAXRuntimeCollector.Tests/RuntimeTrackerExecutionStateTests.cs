using OMAXRuntimeCollector;
using OMAXRuntimeCollector.Runtime;
using Xunit;

namespace OMAXRuntimeCollector.Tests;

public class RuntimeTrackerExecutionStateTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _csvPath;
    private readonly string _logPath;
    private readonly AppLogger _logger;

    public RuntimeTrackerExecutionStateTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "OMAXRuntimeCollectorTests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(_testDirectory);
        _csvPath = Path.Combine(_testDirectory, "runtime.csv");
        _logPath = Path.Combine(_testDirectory, "collector.log");
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
    // HELPER
    // =========================================================

    private RuntimeTracker CreateTracker()
    {
        var writer = new RuntimeCsvWriter(_csvPath, _logger);
        var calculator = new RuntimeCalculator();

        return new RuntimeTracker(writer, _logger, calculator);
    }


    // =========================================================
    // ENDING STATES
    // =========================================================

    [Theory]
    [InlineData("STOPPED")]
    [InlineData("INTERRUPTED")]
    [InlineData("OPTIONAL_STOP")]
    [InlineData("PROGRAM_STOPPED")]
    [InlineData("PROGRAM_COMPLETED")]
    public void EndingExecutionState_StopsRuntime(string executionState)
    {
        var tracker = CreateTracker();
        DateTime start = new DateTime(2026, 8, 25, 10, 0, 0);
        DateTime end = new DateTime(2026, 8, 25, 12, 0, 0);


        // -----------------------------------------------------
        // Start execution.
        // -----------------------------------------------------

        tracker.ProcessLine(
            $"{start:yyyy-MM-ddTHH:mm:ss}Z" +
            "|mode|AUTOMATIC|execution|ACTIVE");


        // -----------------------------------------------------
        // End execution using the state being tested.
        // -----------------------------------------------------

        tracker.ProcessLine(
            $"{end:yyyy-MM-ddTHH:mm:ss}Z" +
            $"|execution|{executionState}");


        // -----------------------------------------------------
        // Runtime should contain exactly 2 hours.
        // -----------------------------------------------------

        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.FromHours(2), runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }


    // =========================================================
    // READY
    // =========================================================

    [Fact]
    public void Ready_DoesNotStartRuntime()
    {
        var tracker = CreateTracker();
        DateTime timestamp = new DateTime(2026, 8, 25, 10, 0, 0);


        tracker.ProcessLine(
            $"{timestamp:yyyy-MM-ddTHH:mm:ss}Z" +
            "|execution|READY");


        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }


    // =========================================================
    // WAIT
    // =========================================================

    [Fact]
    public void Wait_DoesNotStartRuntime()
    {
        var tracker = CreateTracker();
        DateTime timestamp = new DateTime(2026, 8, 25, 10, 0, 0);


        tracker.ProcessLine(
            $"{timestamp:yyyy-MM-ddTHH:mm:ss}Z" +
            "|execution|WAIT");


        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }


    // =========================================================
    // FEED HOLD
    // =========================================================

    [Fact]
    public void FeedHold_DoesNotStartRuntime()
    {
        var tracker = CreateTracker();
        DateTime timestamp = new DateTime(2026, 8, 25, 10, 0, 0);
        
        tracker.ProcessLine(
            $"{timestamp:yyyy-MM-ddTHH:mm:ss}Z" +
            "|execution|FEED_HOLD");


        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }


    // =========================================================
    // UNKNOWN STATE
    // =========================================================

    [Fact]
    public void UnknownExecutionState_DoesNotStartRuntime()
    {
        var tracker = CreateTracker();
        DateTime timestamp = new DateTime(2026, 8, 25, 10, 0, 0);


        tracker.ProcessLine(
            $"{timestamp:yyyy-MM-ddTHH:mm:ss}Z" +
            "|execution|UNKNOWN_STATE");


        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }


    // =========================================================
    // ENDING STATE WITHOUT ACTIVE
    // =========================================================

    [Theory]
    [InlineData("STOPPED")]
    [InlineData("INTERRUPTED")]
    [InlineData("OPTIONAL_STOP")]
    [InlineData("PROGRAM_STOPPED")]
    [InlineData("PROGRAM_COMPLETED")]
    public void EndingExecutionState_WithoutActive_DoesNotCreateRuntime(string executionState)
    {
        var tracker = CreateTracker();
        DateTime timestamp = new DateTime(2026, 8, 25, 10, 0, 0);


        tracker.ProcessLine(
            $"{timestamp:yyyy-MM-ddTHH:mm:ss}Z" +
            $"|execution|{executionState}");


        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }
    
    
    // =========================================================
    // MULTIPLE ACTIVE STATES
    // =========================================================
    
    [Fact]
    public void MultipleActiveStates_DoNotRestartExecution()
    {
        var tracker = CreateTracker();

        DateTime start = new DateTime(2026, 8, 25, 10, 0, 0);
        DateTime end = new DateTime(2026, 8, 25, 12, 0, 0);

        tracker.ProcessLine(
            $"{start:yyyy-MM-ddTHH:mm:ss}Z|execution|ACTIVE");

        tracker.ProcessLine(
            $"{start.AddMinutes(30):yyyy-MM-ddTHH:mm:ss}Z|execution|ACTIVE");

        tracker.ProcessLine(
            $"{end:yyyy-MM-ddTHH:mm:ss}Z|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.FromHours(2), runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }
    
    [Fact]
    public void ConnectionLoss_DiscardsActiveExecution()
    {
        var tracker = CreateTracker();

        DateTime start = new DateTime(2026, 8, 25, 10, 0, 0);

        tracker.ProcessLine(
            $"{start:yyyy-MM-ddTHH:mm:ss}Z|execution|ACTIVE");

        tracker.HandleConnectionLoss();

        tracker.ProcessLine(
            $"{start.AddHours(2):yyyy-MM-ddTHH:mm:ss}Z|execution|STOPPED");

        var runtime = tracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.Zero, runtime.Morning);
        Assert.Equal(TimeSpan.Zero, runtime.Afternoon);
    }
}