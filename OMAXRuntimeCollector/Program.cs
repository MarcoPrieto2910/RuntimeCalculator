using System.Text.Json;
using System.Runtime.InteropServices;
using OMAXRuntimeCollector;
using OMAXRuntimeCollector.OmaxConnection;
using OMAXRuntimeCollector.Runtime;


[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
const int SW_HIDE = 0;

// =============================================================
// LOAD CONFIGURATION
// =============================================================

string configFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(configFile))
{
    Console.WriteLine($"ERROR: {configFile} was not found.");
    return;
}


string configurationJson = await File.ReadAllTextAsync(configFile);
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
    Console.WriteLine(
        $"ERROR: Could not read configuration: " +
        $"{ex.Message}");

    return;
}


if (settings == null)
{
    Console.WriteLine("ERROR: Configuration is empty.");
    return;
}


// =============================================================
// LOGGER
// =============================================================

AppLogger logger = new(settings.Storage.LogPath, settings.TestMode);
if (!settings.TestMode)
{
    IntPtr handle = GetConsoleWindow();
    if (handle != IntPtr.Zero)
        ShowWindow(handle, SW_HIDE);
}

// =============================================================
// STARTUP
// =============================================================

logger.Info("OMAX Runtime Collector");
logger.Info("");
logger.Info("========================================");
logger.Info("OMAX Runtime Collector starting.");


// =============================================================
// CSV WRITER
// =============================================================

RuntimeCsvWriter csvWriter = new(settings.Storage.CsvPath, logger);


// =============================================================
// RUNTIME TRACKER
// =============================================================

RuntimeCalculator runtimeCalculator = new();
RuntimeTracker runtimeTracker = new(csvWriter, logger, runtimeCalculator);

// =============================================================
// CANCELLATION
// =============================================================

using CancellationTokenSource cancellationSource = new();
CancellationToken cancellationToken = cancellationSource.Token;


// -------------------------------------------------------------
// Ctrl+C
// -------------------------------------------------------------

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    logger.Info("Shutdown requested.");
    cancellationSource.Cancel();
};


// =============================================================
// BOUNDARY MONITOR
// =============================================================

Task boundaryTask = MonitorTimeBoundariesAsync(runtimeTracker, logger, cancellationToken);

// =============================================================
// OMAX CLIENT
// =============================================================

OmaxClient client = new(settings.Omax, runtimeTracker, logger);

try
{
    await client.RunAsync(cancellationToken);
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
    cancellationSource.Cancel();

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

        logger.Info($"Next runtime boundary: " +
                          $"{nextBoundary:yyyy-MM-dd HH:mm:ss}");


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