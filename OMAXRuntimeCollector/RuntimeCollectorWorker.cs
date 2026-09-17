using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OMAXRuntimeCollector.OmaxConnection;
using OMAXRuntimeCollector.Runtime;
using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector;

/// <summary>
/// Runs the OMAX Runtime Collector as a hosted background service.
/// Coordinates configuration, runtime tracking, CSV persistence,
/// time-boundary processing, and the OMAX connection.
/// </summary>
public class RuntimeCollectorWorker : BackgroundService
{
    
    /// <summary>
    /// Starts the runtime collector and runs it until cancellation or
    /// a fatal error occurs.
    /// </summary>
    /// <param name="stoppingToken">
    /// Token used to signal that the hosted service is shutting down.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string configFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configFile))
        {
            Console.WriteLine($"ERROR: {configFile} was not found.");
            return;
        }
        
        string configurationJson = await File.ReadAllTextAsync(configFile, stoppingToken);
        OmaxSettings? settings;
        
        try
        {
            settings =
                JsonSerializer.Deserialize<OmaxSettings>(
                    configurationJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Could not read configuration: " + $"{ex.Message}");
            return;
        }


        if (settings == null)
        {
            Console.WriteLine("ERROR: Configuration is empty.");
            return;
        }

        List<string> configurationErrors = ConfigurationValidator.Validate(settings);
        if (configurationErrors.Count > 0)
        {
            Console.WriteLine("ERROR: Configuration validation failed.");
            Console.WriteLine();

            foreach (string error in configurationErrors)
                Console.WriteLine($"- {error}");

            return;
        }

        AppLogger logger = new(settings.Storage.LogPath, settings.TestMode);
        Version? applicationVersion = Assembly.GetExecutingAssembly().GetName().Version;
        
        logger.Info("OMAX Runtime Collector");
        logger.Info("========================================");
        logger.Info("Startup diagnostics");
        logger.Info("----------------------------------------");
        logger.Info($"Version: {applicationVersion}");
        logger.Info($"Machine ID: {settings.MachineId}");
        logger.Info($"OMAX Host: {settings.Omax.Host}");
        logger.Info($"OMAX Port: {settings.Omax.Port}");
        logger.Info($"Shared CSV Path: { Environment.ExpandEnvironmentVariables(settings.Storage.SharedCsvPath) }");
        logger.Info($"Local CSV Path: { Environment.ExpandEnvironmentVariables(settings.Storage.LocalCsvPath) }");
        logger.Info($"Log Path: { Environment.ExpandEnvironmentVariables(settings.Storage.LogPath) }");
        logger.Info("========================================");
        logger.Info("OMAX Runtime Collector starting.");
        
        
        RuntimeLocalCsvWriter localCsvWriter = new(settings.Storage.LocalCsvPath, logger, settings.MachineId);
        RuntimeSharedCsvWriter sharedCsvWriter = new(settings.Storage.SharedCsvPath, logger, settings.MachineId); 
        IReadOnlyList<IRuntimeWriter> runtimeWriters = [ localCsvWriter, sharedCsvWriter ];
        
        RuntimeCalculator runtimeCalculator = new();
        RuntimeTracker runtimeTracker = new(runtimeWriters, logger, runtimeCalculator);

        // Run the boundary monitor alongside the OMAX connection so that
        // runtime can be split at 14:00 and midnight even while the machine
        // remains in an active execution state.
        Task boundaryTask = MonitorTimeBoundariesAsync(runtimeTracker, logger, stoppingToken);
        
        
        OmaxClient client = new(settings.Omax, runtimeTracker, logger);

        try
        {
            await client.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            logger.Error($"Fatal error: {ex}");
        }
        finally
        {
            try
            {
                await boundaryTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }
        
        
        (TimeSpan morning, TimeSpan afternoon) = runtimeTracker.GetCurrentRuntime();

        logger.Info("--------------------------------");
        logger.Info("Runtime summary");
        logger.Info("--------------------------------");
        logger.Info($"05:00 - 14:00 : " + $"{RuntimeTracker.FormatDuration(morning)}");
        logger.Info($"14:00 - 00:00 : " + $"{RuntimeTracker.FormatDuration(afternoon)}");
        logger.Info("--------------------------------");
        logger.Info("OMAX Runtime Collector stopped.");
    }
    
    
    /// <summary>
    /// Monitors the next 14:00 and midnight boundaries and notifies the
    /// runtime tracker when each boundary is reached.
    /// </summary>
    /// <param name="runtimeTracker">
    /// Runtime tracker that processes accounting boundaries.
    /// </param>
    /// <param name="logger">Logger used to record the next boundary.</param>
    /// <param name="cancellationToken">
    /// Token used to stop the boundary monitor during service shutdown.
    /// </param>
    private static async Task MonitorTimeBoundariesAsync(RuntimeTracker runtimeTracker, AppLogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;
            DateTime today14 = now.Date.AddHours(14);
            DateTime tomorrow00 = now.Date.AddDays(1);
            DateTime nextBoundary;
            
            if (now < today14)
                nextBoundary = today14;
            else
                nextBoundary = tomorrow00;
            

            TimeSpan delay = nextBoundary - now;
            logger.Info($"Next runtime boundary: " + $"{nextBoundary:yyyy-MM-dd HH:mm:ss}");

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;
            
            
            // Pass the boundary we actually waited for instead of calling
            // DateTime.Now again. This avoids missing the boundary if the
            // task resumes slightly after 14:00 or midnight.
            runtimeTracker.ProcessTimeBoundary(nextBoundary);
        }
    }
}