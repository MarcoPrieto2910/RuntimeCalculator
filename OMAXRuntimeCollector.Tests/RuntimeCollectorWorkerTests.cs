using System.Net;
using System.Net.Sockets;

namespace OMAXRuntimeCollector.Tests;

[Collection("Console Tests")]
public class RuntimeCollectorWorkerTests : IDisposable
{
    private readonly string _testDirectory;

    public RuntimeCollectorWorkerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
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
    public async Task ExecuteAsync_MissingConfiguration_ExitsCleanly()
    {
        var configPath = Path.Combine(_testDirectory, "missing-appsettings.json");
        RuntimeCollectorWorker worker = new(configPath);
        var originalOutput = Console.Out;
        await using StringWriter output = new();

        try
        {
            Console.SetOut(output);

            await worker.StartAsync(CancellationToken.None);
            await worker.StopAsync(CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        Assert.Contains("was not found.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ExitsCleanly()
    {
        var configPath = Path.Combine(_testDirectory, "appsettings.json");
        await File.WriteAllTextAsync(configPath, "{ invalid json");
        RuntimeCollectorWorker worker = new(configPath);
        var originalOutput = Console.Out;
        await using StringWriter output = new();

        try
        {
            Console.SetOut(output);

            await worker.StartAsync(CancellationToken.None);
            await WaitForConsoleOutputAsync(output, "ERROR: Could not read configuration:");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidConfiguration_StartsCollector()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var localCsvPath = Path.Combine(_testDirectory, "runtime.csv");
        var sharedCsvPath = Path.Combine(_testDirectory, "shared-runtime.csv");
        var logPath = Path.Combine(_testDirectory, "collector.log");
        var configPath = Path.Combine(_testDirectory, "appsettings.json");

        var configuration = $$"""
                              {
                                "MachineId": "TEST-01",
                                "Omax": {
                                  "Host": "127.0.0.1",
                                  "Port": {{port}},
                                  "ReconnectDelaySeconds": 1
                                },
                                "Storage": {
                                  "LocalCsvPath": "{{localCsvPath.Replace("\\", @"\\")}}",
                                  "SharedCsvPath": "{{sharedCsvPath.Replace("\\", @"\\")}}",
                                  "LogPath": "{{logPath.Replace("\\", @"\\")}}"
                                },
                                "TestMode": false
                              }
                              """;

        await File.WriteAllTextAsync(configPath, configuration);

        RuntimeCollectorWorker worker = new(configPath);
        using CancellationTokenSource cancellationTokenSource = new();

        var startTask = worker.StartAsync(cancellationTokenSource.Token);

        // Successfully accepting a connection proves that the worker
        // loaded, validated, and started the OMAX client.
        using var connection =
            await listener.AcceptTcpClientAsync(
                cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();

        await startTask;
        await worker.StopAsync(CancellationToken.None);

        Assert.True(startTask.IsCompleted);
    }


    #region MyRegion
        private static async Task WaitForConsoleOutputAsync(StringWriter output, string expectedMessage)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);

            while (DateTime.UtcNow < timeout)
            {
                if (output.ToString().Contains(expectedMessage, StringComparison.Ordinal))
                    return;
                
                await Task.Delay(10);
            }

            Assert.Contains(expectedMessage, output.ToString(), StringComparison.Ordinal);
        }
        
        private static async Task WaitForLogMessageAsync(string logPath, string expectedMessage)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2);

            while (DateTime.UtcNow < timeout)
            {
                if (File.Exists(logPath))
                {
                    var log = await File.ReadAllTextAsync(logPath);

                    if (log.Contains(expectedMessage, StringComparison.Ordinal))
                        return;
                }

                await Task.Delay(10);
            }

            var actualLog = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath) : string.Empty;
            Assert.Contains(expectedMessage, actualLog, StringComparison.Ordinal);
        }
    #endregion
}