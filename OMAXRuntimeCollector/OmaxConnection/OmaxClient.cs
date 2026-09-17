using System.Net.Sockets;
using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.OmaxConnection;


/// <summary>
/// Maintains a TCP connection to the OMAX MTConnect stream and forwards
/// received data to the <see cref="RuntimeTracker"/>.
/// </summary>
public class OmaxClient
{
    private readonly OmaxConnectionSettings _settings;
    private readonly RuntimeTracker _runtimeTracker;
    private readonly AppLogger _logger;


    /// <summary>
    /// Initializes a new instance of the <see cref="OmaxClient"/> class.
    /// </summary>
    /// <param name="settings">The OMAX connection settings.</param>
    /// <param name="runtimeTracker">
    /// The runtime tracker that processes received OMAX data.
    /// </param>
    /// <param name="logger">The application logger.</param>
    public OmaxClient(OmaxConnectionSettings settings, RuntimeTracker runtimeTracker, AppLogger logger)
    {
        _settings = settings;
        _runtimeTracker = runtimeTracker;
        _logger = logger;
    }


    /// <summary>
    /// Continuously connects to the OMAX MTConnect stream, reads incoming data,
    /// and reconnects when the connection is lost.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token used to stop the connection and reconnect loop.
    /// </param>
    /// <returns>A task representing the asynchronous connection loop.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        bool wasConnected = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(cancellationToken, wasConnected);
                wasConnected = true;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"Unable to connect to OMAX at " +
                    $"{_settings.Host}:{_settings.Port}: " +
                    $"{ex.Message}");
            }


            if (cancellationToken.IsCancellationRequested)
                return;


            // The runtime tracker cannot determine what happened while
            // the OMAX connection was unavailable, so the active execution
            // state must be discarded.
            _runtimeTracker.HandleConnectionLoss();
            _logger.Warning($"Connection to OMAX lost. " +
                            $"Retrying in " +
                            $"{_settings.ReconnectDelaySeconds} seconds.");
            
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }


    /// <summary>
    /// Establishes a TCP connection to the OMAX computer and reads its
    /// MTConnect stream until the connection is closed, an error occurs,
    /// or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token used to cancel the connection and stream reading.
    /// </param>
    /// <param name="wasConnected">
    /// Indicates whether the client has successfully connected before.
    /// This is used to distinguish the initial connection message from
    /// a restored connection message.
    /// </param>
    /// <returns>
    /// A task that completes when the connection is closed, an error occurs,
    /// or cancellation is requested.
    /// </returns>
    private async Task ConnectAndReadAsync(CancellationToken cancellationToken, bool wasConnected)
    {
        _logger.Info($"Connecting to OMAX at " + $"{_settings.Host}:{_settings.Port}");
        using TcpClient client = new();

        await client.ConnectAsync(_settings.Host, _settings.Port, cancellationToken);
        if (wasConnected)
            _logger.Info("Connection to OMAX restored.");
        else
            _logger.Info("Connected to OMAX.");

        await using NetworkStream stream = client.GetStream();
        using StreamReader reader = new(stream);

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
                _logger.Error($"Error while reading from OMAX at " +
                              $"{_settings.Host}:{_settings.Port}: " +
                              $"{ex.Message}");
                return;
            }

            // A null line means the remote OMAX server closed the TCP connection.
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