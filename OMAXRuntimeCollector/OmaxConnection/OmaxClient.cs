using System.Net.Sockets;
using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.OmaxConnection;

public class OmaxClient
{
    private readonly OmaxConnectionSettings _settings;
    private readonly RuntimeTracker _runtimeTracker;
    private readonly AppLogger _logger;


    public OmaxClient(OmaxConnectionSettings settings, RuntimeTracker runtimeTracker, AppLogger logger)
    {
        _settings = settings;
        _runtimeTracker = runtimeTracker;
        _logger = logger;
    }


    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error($"Connection error: {ex.Message}");
            }


            if (cancellationToken.IsCancellationRequested)
                return;


            // -------------------------------------------------
            // The connection has been lost.
            // -------------------------------------------------

            _runtimeTracker.HandleConnectionLoss();
            _logger.Warning($"Connection to OMAX lost. " +
                            $"Retrying in " +
                            $"{_settings.ReconnectDelaySeconds} seconds.");


            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }


    private async Task ConnectAndReadAsync(CancellationToken cancellationToken)
    {
        _logger.Info(
            $"Connecting to OMAX at " +
            $"{_settings.Host}:{_settings.Port}");


        using TcpClient client = new();


        // -----------------------------------------------------
        // Connect directly to the TCP port.
        // -----------------------------------------------------

        await client.ConnectAsync(_settings.Host, _settings.Port, cancellationToken);
        _logger.Info("Connected to OMAX.");


        // -----------------------------------------------------
        // Get the raw TCP stream.
        // -----------------------------------------------------

        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream);


        // -----------------------------------------------------
        // Read the OMAX stream forever.
        // -----------------------------------------------------

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;

            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    $"Error reading OMAX stream: " +
                    $"{ex.Message}");

                return;
            }


            // -------------------------------------------------
            // null means the server closed the connection.
            // -------------------------------------------------

            if (line == null)
            {
                _logger.Warning("OMAX closed the connection.");
                return;
            }


            if (string.IsNullOrWhiteSpace(line))
                continue;


            _runtimeTracker.ProcessLine(line);
        }
    }
}