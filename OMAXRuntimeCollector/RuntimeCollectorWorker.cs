using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OMAXRuntimeCollector.OmaxConnection;
using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector;

public class RuntimeCollectorWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // =============================================================
        // LOAD CONFIGURATION
        // =============================================================

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
        
        // =============================================================
        // VALIDATE CONFIGURATION
        // =============================================================

        List<string> configurationErrors = ConfigurationValidator.Validate(settings);

        if (configurationErrors.Count > 0)
        {
            Console.WriteLine("ERROR: Configuration validation failed.");
            Console.WriteLine();

            foreach (string error in configurationErrors)
            {
                Console.WriteLine($"- {error}");
            }

            return;
        }


        // =============================================================
        // LOGGER & STARTUP
        // =============================================================

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
        logger.Info($"CSV Path: { Environment.ExpandEnvironmentVariables(settings.Storage.CsvPath) }");
        logger.Info($"Log Path: { Environment.ExpandEnvironmentVariables(settings.Storage.LogPath) }");
        logger.Info("========================================");
        logger.Info("OMAX Runtime Collector starting.");

        
        // =============================================================
        // CSV WRITER
        // =============================================================

        RuntimeCsvWriter csvWriter = new(settings.Storage.CsvPath, logger, settings.MachineId);


        // =============================================================
        // RUNTIME TRACKER
        // =============================================================

        RuntimeCalculator runtimeCalculator = new();
        RuntimeTracker runtimeTracker = new(csvWriter, logger, runtimeCalculator);

        // =============================================================
        // BOUNDARY MONITOR
        // =============================================================

        Task boundaryTask = MonitorTimeBoundariesAsync(runtimeTracker, logger, stoppingToken);

        // =============================================================
        // OMAX CLIENT
        // =============================================================

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


        // =============================================================
        // FINAL SUMMARY
        // =============================================================

        (TimeSpan morning, TimeSpan afternoon) = runtimeTracker.GetCurrentRuntime();

        logger.Info("--------------------------------");
        logger.Info("Runtime summary");
        logger.Info("--------------------------------");
        logger.Info($"05:00 - 14:00 : " + $"{RuntimeTracker.FormatDuration(morning)}");
        logger.Info($"14:00 - 00:00 : " + $"{RuntimeTracker.FormatDuration(afternoon)}");
        logger.Info("--------------------------------");
        logger.Info("OMAX Runtime Collector stopped.");
    }
    
    // =============================================================
    // TIME BOUNDARY MONITOR
    // =============================================================
    static async Task MonitorTimeBoundariesAsync(RuntimeTracker runtimeTracker, AppLogger logger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;
            DateTime today14 = now.Date.AddHours(14);
            DateTime tomorrow00 = now.Date.AddDays(1);
                
            DateTime nextBoundary;


            // -----------------------------------------------------
            // Before 14:00
            // -----------------------------------------------------

            if (now < today14)
            {
                nextBoundary = today14;
            }

            // -----------------------------------------------------
            // 14:00 or later
            // -----------------------------------------------------

            else
            {
                nextBoundary = tomorrow00;
            }


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


            // -----------------------------------------------------
            // IMPORTANT:
            //
            // Pass the actual boundary that we waited for.
            //
            // This is more reliable than calling DateTime.Now
            // and checking whether Hour == 14 or 0.
            // -----------------------------------------------------

            runtimeTracker.ProcessTimeBoundary(nextBoundary);
        }
    }
}