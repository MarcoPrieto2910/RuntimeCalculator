using System.Net;
using System.Net.Sockets;
using OMAXRuntimeCollector.OmaxConnection;
using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.Tests.OmaxConnection;

public class OmaxClientTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly AppLogger _logger;
    private readonly string _logPath;
    private readonly RuntimeTracker _runtimeTracker;

    public OmaxClientTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "OMAXRuntimeCollectorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _logPath = Path.Combine(_testDirectory, "test.log");
        _logger = new AppLogger(_logPath, testMode: false);
        _runtimeTracker = new RuntimeTracker([], _logger, new RuntimeCalculator());
    }

    public void Dispose()
    {
        _logger.Dispose();

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
    public async Task RunAsync_ReceivesDataFromServer_ProcessesRuntime()
    {
        using var listener = CreateListener();
        var port = GetPort(listener);
        var settings = new OmaxConnectionSettings
        {
            Host = "localhost",
            Port = port,
            ReconnectDelaySeconds = 1
        };

        var client = new OmaxClient(settings, _runtimeTracker, _logger);
        using CancellationTokenSource cancellationTokenSource = new();
        var clientTask = client.RunAsync(cancellationTokenSource.Token);
        using var serverConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        await using var stream = serverConnection.GetStream();

        await using StreamWriter writer = new(stream);
        writer.AutoFlush = true;

        await writer.WriteLineAsync("2026-08-25T10:00:00Z|execution|ACTIVE");
        await writer.WriteLineAsync("2026-08-25T12:00:00Z|execution|STOPPED");
        await WaitForRuntimeAsync(TimeSpan.FromHours(2), TimeSpan.Zero);
        await cancellationTokenSource.CancelAsync();
        await clientTask;

        var (morning, afternoon) = _runtimeTracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.FromHours(2), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }

    [Fact]
    public async Task RunAsync_IgnoresEmptyLines()
    {
        using var listener = CreateListener();
        var port = GetPort(listener);
        var settings = new OmaxConnectionSettings
        {
            Host = "localhost",
            Port = port,
            ReconnectDelaySeconds = 1
        };

        var client = new OmaxClient(settings, _runtimeTracker, _logger);
        using CancellationTokenSource cancellationTokenSource = new();
        var clientTask = client.RunAsync(cancellationTokenSource.Token);
        using var serverConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        await using var stream = serverConnection.GetStream();

        await using StreamWriter writer = new(stream);
        writer.AutoFlush = true;

        await writer.WriteLineAsync("");
        await writer.WriteLineAsync("   ");
        await writer.WriteLineAsync("2026-08-25T10:00:00Z|execution|ACTIVE");
        await writer.WriteLineAsync("2026-08-25T10:30:00Z|execution|STOPPED");
        await WaitForRuntimeAsync(TimeSpan.FromMinutes(30), TimeSpan.Zero);
        await cancellationTokenSource.CancelAsync();
        await clientTask;

        var (morning, afternoon) = _runtimeTracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.FromMinutes(30), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }

    [Fact]
    public async Task RunAsync_Cancellation_StopsClient()
    {
        using var listener = CreateListener();
        var port = GetPort(listener);
        var settings = new OmaxConnectionSettings
        {
            Host = "localhost",
            Port = port,
            ReconnectDelaySeconds = 1
        };

        var client = new OmaxClient(settings, _runtimeTracker, _logger);
        using CancellationTokenSource cancellationTokenSource = new();
        var clientTask = client.RunAsync(cancellationTokenSource.Token);
        await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();
        await clientTask;

        Assert.True(clientTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RunAsync_ConnectionClosed_Reconnects()
    {
        using var listener = CreateListener();
        var port = GetPort(listener);

        OmaxConnectionSettings settings = new()
        {
            Host = "127.0.0.1",
            Port = port,
            ReconnectDelaySeconds = 1
        };

        OmaxClient client = new(settings, _runtimeTracker, _logger);
        using CancellationTokenSource cancellationTokenSource = new();

        var clientTask = client.RunAsync(cancellationTokenSource.Token);

        // First connection.
        using var firstConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);

        firstConnection.Close();

        // The second connection proves that the client detected the
        // closed connection and entered its reconnect loop.
        using var secondConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();
        await clientTask;

        Assert.True(clientTask.IsCompletedSuccessfully);
    }
    
    [Fact]
    public async Task RunAsync_ConnectionLost_ReconnectsAndProcessesData()
    {
        using var listener = CreateListener();
        var port = GetPort(listener);

        OmaxConnectionSettings settings = new()
        {
            Host = "127.0.0.1",
            Port = port,
            ReconnectDelaySeconds = 1
        };

        OmaxClient client = new(settings, _runtimeTracker, _logger);
        using CancellationTokenSource cancellationTokenSource = new();
        var clientTask = client.RunAsync(cancellationTokenSource.Token);

        // First connection.
        using var firstConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        firstConnection.Close();

        // The client should reconnect after the first connection is lost.
        using var secondConnection = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        await using var stream = secondConnection.GetStream();
        await using StreamWriter writer = new(stream);
        writer.AutoFlush = true;

        await writer.WriteLineAsync("2026-08-25T10:00:00Z|execution|ACTIVE");
        await writer.WriteLineAsync("2026-08-25T10:30:00Z|execution|STOPPED");
        await WaitForRuntimeAsync(TimeSpan.FromMinutes(30), TimeSpan.Zero);
        await cancellationTokenSource.CancelAsync();
        await clientTask;

        var (morning, afternoon) = _runtimeTracker.GetCurrentRuntime();

        Assert.Equal(TimeSpan.FromMinutes(30), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }
    
    
    #region HELPERS
        private static TcpListener CreateListener()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return listener;
        }

        private static int GetPort(TcpListener listener)
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        
        private async Task WaitForRuntimeAsync(TimeSpan expectedMorningRuntime, TimeSpan expectedAfternoonRuntime)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);

            while (DateTime.UtcNow < timeout)
            {
                var (morning, afternoon) = _runtimeTracker.GetCurrentRuntime();
                if (morning == expectedMorningRuntime && afternoon == expectedAfternoonRuntime)
                    return;

                await Task.Delay(10);
            }

            var actual = _runtimeTracker.GetCurrentRuntime();

            Assert.Equal(expectedMorningRuntime, actual.Morning);
            Assert.Equal(expectedAfternoonRuntime, actual.Afternoon);
        }
        
        private async Task WaitForLogMessageAsync(string message)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);

            while (DateTime.UtcNow < timeout)
            {
                if (File.Exists(_logPath))
                {
                    var log = await File.ReadAllTextAsync(_logPath);

                    if (log.Contains(message, StringComparison.Ordinal))
                        return;
                }

                await Task.Delay(10);
            }

            var actualLog = File.Exists(_logPath) ? await File.ReadAllTextAsync(_logPath) : string.Empty;
            Assert.Contains(message, actualLog);
        }
    #endregion
}