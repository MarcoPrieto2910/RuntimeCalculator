using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.OmaxConnection;

public class OmaxClient
{
    private readonly OmaxConnectionSettings _settings;
    private readonly RuntimeTracker _runtimeTracker;
    private readonly AppLogger _logger;

    private readonly HttpClient _httpClient;


    public OmaxClient(OmaxConnectionSettings settings, RuntimeTracker runtimeTracker, AppLogger logger)
    {
        _settings = settings;
        _runtimeTracker = runtimeTracker;
        _logger = logger;

        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }


    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string url =
            $"http://{_settings.Host}:" +
            $"{_settings.Port}" +
            $"{_settings.Endpoint}";


        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(url, cancellationToken);
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


            _logger.Warning(
                $"Connection to OMAX lost. " +
                $"Retrying in " +
                $"{_settings.ReconnectDelaySeconds} seconds.");


            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _settings.ReconnectDelaySeconds),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }


    private async Task ConnectAndReadAsync(string url, CancellationToken cancellationToken)
    {
        _logger.Info($"Connecting to OMAX at {url}");


        using HttpResponseMessage response =
            await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);


        response.EnsureSuccessStatusCode();


        _logger.Info("Connected to OMAX.");


        await using Stream stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);


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
            // null means the server closed the stream.
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