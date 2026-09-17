using OMAXRuntimeCollector.Runtime;
using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Tests.Runtime.Tracker;

public class RuntimeTrackerWriterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly AppLogger _logger;
    private readonly RuntimeCalculator _runtimeCalculator;

    public RuntimeTrackerWriterTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "OMAXRuntimeCollectorTests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(_testDirectory);

        string logPath = Path.Combine(_testDirectory, "test.log");
        _logger = new AppLogger(logPath);
        _runtimeCalculator = new RuntimeCalculator();
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

    private class TestRuntimeWriter : IRuntimeWriter
    {
        public bool IsCritical { get; }

        public bool MorningSaveCalled { get; private set; }
        public bool AfternoonSaveCalled { get; private set; }

        public bool ThrowOnMorningSave { get; set; }
        public bool ThrowOnAfternoonSave { get; set; }

        public TestRuntimeWriter(bool isCritical)
        {
            IsCritical = isCritical;
        }

        public void SaveMorningRuntime(DateTime date, TimeSpan runtime)
        {
            MorningSaveCalled = true;

            if (ThrowOnMorningSave)
                throw new IOException("Test morning save failure.");
        }

        public void SaveAfternoonRuntime(DateTime date, TimeSpan runtime)
        {
            AfternoonSaveCalled = true;

            if (ThrowOnAfternoonSave)
                throw new IOException("Test afternoon save failure.");
        }
    }
    
    
    [Fact]
    public void ProcessTimeBoundary_NonCriticalWriterFails_DoesNotThrow()
    {
        var writer = new TestRuntimeWriter(isCritical: false)
        {
            ThrowOnMorningSave = true
        };

        var tracker = new RuntimeTracker(
            new[] { writer },
            _logger,
            _runtimeCalculator);

        Exception? exception = Record.Exception(() =>
        {
            tracker.ProcessTimeBoundary(new DateTime(2026, 9, 8, 14, 0, 0));
        });

        Assert.Null(exception);
        Assert.True(writer.MorningSaveCalled);
    }
    
    [Fact]
    public void ProcessTimeBoundary_CriticalWriterFails_Throws()
    {
        var writer = new TestRuntimeWriter(isCritical: true)
        {
            ThrowOnMorningSave = true
        };

        var tracker = new RuntimeTracker(
            new[] { writer },
            _logger,
            _runtimeCalculator);

        Assert.Throws<IOException>(() =>
        {
            tracker.ProcessTimeBoundary(new DateTime(2026, 9, 8, 14, 0, 0));
        });

        Assert.True(writer.MorningSaveCalled);
    }
    
    [Fact]
    public void ProcessTimeBoundary_NonCriticalWriterFails_CriticalWriterStillRuns()
    {
        var sharedWriter = new TestRuntimeWriter(isCritical: false)
        {
            ThrowOnMorningSave = true
        };

        var localWriter = new TestRuntimeWriter(isCritical: true);

        var tracker = new RuntimeTracker(
            new IRuntimeWriter[]
            {
                sharedWriter,
                localWriter
            },
            _logger,
            _runtimeCalculator);

        tracker.ProcessTimeBoundary(new DateTime(2026, 9, 8, 14, 0, 0));

        Assert.True(sharedWriter.MorningSaveCalled);
        Assert.True(localWriter.MorningSaveCalled);
    }
    
    [Fact]
    public void ProcessTimeBoundary_CriticalWriterFails_SubsequentWritersAreNotCalled()
    {
        var localWriter = new TestRuntimeWriter(isCritical: true)
        {
            ThrowOnMorningSave = true
        };

        var anotherWriter = new TestRuntimeWriter(isCritical: false);

        var tracker = new RuntimeTracker(
            new IRuntimeWriter[]
            {
                localWriter,
                anotherWriter
            },
            _logger,
            _runtimeCalculator);

        Assert.Throws<IOException>(() =>
        {
            tracker.ProcessTimeBoundary(new DateTime(2026, 9, 8, 14, 0, 0));
        });

        Assert.True(localWriter.MorningSaveCalled);
        Assert.False(anotherWriter.MorningSaveCalled);
    }
}