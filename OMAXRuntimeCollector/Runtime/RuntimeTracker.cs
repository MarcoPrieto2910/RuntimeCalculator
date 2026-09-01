namespace OMAXRuntimeCollector.Runtime;

public class RuntimeTracker
{
    private readonly RuntimeCsvWriter _csvWriter;
    private readonly AppLogger _logger;
    private readonly RuntimeCalculator _runtimeCalculator;
    
    private static readonly HashSet<string> EndingExecutionStates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "INTERRUPTED",
            "STOPPED",
            "OPTIONAL_STOP",
            "PROGRAM_STOPPED",
            "PROGRAM_COMPLETED"
        };

    private readonly object _stateLock = new();

    private TimeSpan _morningRuntime = TimeSpan.Zero;
    private TimeSpan _afternoonRuntime = TimeSpan.Zero;
    private DateTime? _executionStart;


    public RuntimeTracker(RuntimeCsvWriter csvWriter, AppLogger logger, RuntimeCalculator runtimeCalculator)
    {
        _csvWriter = csvWriter;
        _logger = logger;
        _runtimeCalculator = runtimeCalculator;
    }


    // =========================================================
    // PROCESS MACHINE LOG
    // =========================================================

    public void ProcessLine(string line)
    {
        string[] fields =
            line.Split('|');

        if (fields.Length < 3)
            return;


        // -----------------------------------------------------
        // OMAX timestamps are UTC.
        // Convert explicitly to local time.
        // -----------------------------------------------------

        if (!DateTimeOffset.TryParse(
                fields[0],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestampUtc))
        {
            _logger.Warning(
                $"Invalid timestamp received: {fields[0]}");

            return;
        }


        DateTime timestamp = timestampUtc.LocalDateTime;


        // -----------------------------------------------------
        // Find execution state.
        // -----------------------------------------------------

        for (int i = 1; i < fields.Length - 1; i += 2)
        {
            string name = fields[i];
            string value = fields[i + 1];


            if (!string.Equals(
                    name,
                    "execution",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }


            lock (_stateLock)
            {
                ProcessExecutionState(value, timestamp);
            }
        }
    }


    // =========================================================
    // PROCESS EXECUTION STATE
    // =========================================================

    private void ProcessExecutionState(string value, DateTime timestamp)
    {
        // =====================================================
        // MACHINE STARTED
        // =====================================================

        if (string.Equals(
                value,
                "ACTIVE",
                StringComparison.OrdinalIgnoreCase))
        {
            if (_executionStart == null)
            {
                _executionStart = timestamp;

                _logger.Info(
                    $"Machine started executing at " +
                    $"{timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            return;
        }


        // =====================================================
        // MACHINE STOPPED
        // =====================================================

        if (IsExecutionEndingState(value))
        {
            if (_executionStart != null)
            {
                DateTime start = _executionStart.Value;

                _logger.Info(
                    $"Machine execution ended with state " +
                    $"{value} at " +
                    $"{timestamp:yyyy-MM-dd HH:mm:ss}");

                AddRuntime(start, timestamp);

                _executionStart = null;
            }
            
            return; // Normal exit
        }


        // =====================================================
        // OTHER POSSIBLE EXECUTION STATES
        // =====================================================
        _logger.Info($"Execution state received: {value}");
    }


    // =========================================================
    // HANDLE 14:00 / MIDNIGHT
    // =========================================================

    public void ProcessTimeBoundary(DateTime boundary)
    {
        lock (_stateLock)
        {
            // =================================================
            // 14:00
            // =================================================

            if (boundary.Hour == 14)
            {
                ProcessAfternoonBoundary(boundary);
                return;
            }


            // =================================================
            // MIDNIGHT
            // =================================================

            if (boundary.Hour == 0)
            {
                ProcessMidnightBoundary(boundary);
            }
        }
    }


    // =========================================================
    // HANDLE 14:00
    // =========================================================

    private void ProcessAfternoonBoundary(DateTime boundary)
    {
        _logger.Info("14:00 runtime boundary reached.");


        // -----------------------------------------------------
        // If execution is still active, split it at 14:00.
        // -----------------------------------------------------

        if (_executionStart != null)
        {
            AddRuntime(_executionStart.Value, boundary);


            // The machine did NOT stop.
            //
            // We only move our accounting starting point
            // to 14:00.

            _executionStart = boundary;


            _logger.Info(
                "Machine is still active. " +
                "Continuing into afternoon period.");
        }


        _logger.Info(
            $"Morning runtime: " +
            $"{FormatDuration(_morningRuntime)}");


        _csvWriter.SaveMorningRuntime(boundary.Date, _morningRuntime);
        _logger.Info("Morning runtime saved.");
    }


    // =========================================================
    // HANDLE MIDNIGHT
    // =========================================================

    private void ProcessMidnightBoundary(DateTime boundary)
    {
        _logger.Info("00:00 runtime boundary reached.");


        // -----------------------------------------------------
        // If machine is still running, close the previous
        // accounting period at midnight.
        // -----------------------------------------------------

        if (_executionStart != null)
        {
            AddRuntime(_executionStart.Value, boundary);


            // Machine did not stop.
            //
            // Begin the new accounting day.

            _executionStart = boundary;


            _logger.Info("Machine is still active. " + "Continuing into new day.");
        }


        DateTime previousDay = boundary.Date.AddDays(-1);


        _logger.Info(
            $"Afternoon runtime: " +
            $"{FormatDuration(_afternoonRuntime)}");


        _csvWriter.SaveAfternoonRuntime(previousDay, _afternoonRuntime);
        _logger.Info("Afternoon runtime saved.");


        // -----------------------------------------------------
        // Reset counters for the new accounting day.
        // -----------------------------------------------------

        _morningRuntime = TimeSpan.Zero;
        _afternoonRuntime = TimeSpan.Zero;


        _logger.Info(
            $"Starting new runtime day: " +
            $"{boundary:yyyy-MM-dd}");
    }


    // =========================================================
    // HANDLE LOST CONNECTION
    // =========================================================

    public void HandleConnectionLoss()
    {
        lock (_stateLock)
        {
            if (_executionStart != null)
            {
                _logger.Warning(
                    "Connection lost while machine execution " +
                    "was active. Current execution will no " +
                    "longer be tracked until a new ACTIVE " +
                    "event is received.");
            }


            // -------------------------------------------------
            // We cannot know what happened while disconnected.
            //
            // Therefore, discard the current execution start.
            // -------------------------------------------------

            _executionStart = null;
        }
    }


    // =========================================================
    // RUNTIME CALCULATION
    // =========================================================

    private void AddRuntime(DateTime start, DateTime end)
    {
        if (end <= start)
            return;


        _logger.Info(
            $"Execution duration: " +
            $"{FormatDuration(end - start)}");


        // -----------------------------------------------------
        // Delegate the actual calculation to RuntimeCalculator.
        // -----------------------------------------------------

        (TimeSpan morning, TimeSpan afternoon) =
            _runtimeCalculator.Calculate(start, end);


        // -----------------------------------------------------
        // Add the calculated values to our current totals.
        // -----------------------------------------------------

        if (morning > TimeSpan.Zero)
        {
            _morningRuntime += morning;

            _logger.Info(
                $"Morning runtime +" +
                $"{FormatDuration(morning)}");
        }


        if (afternoon > TimeSpan.Zero)
        {
            _afternoonRuntime += afternoon;

            _logger.Info(
                $"Afternoon runtime +" +
                $"{FormatDuration(afternoon)}");
        }
    }


    // =========================================================
    // GET CURRENT VALUES
    // =========================================================

    public (TimeSpan Morning, TimeSpan Afternoon) GetCurrentRuntime()
    {
        lock (_stateLock)
        {
            return (_morningRuntime, _afternoonRuntime);
        }
    }


    // =========================================================
    // HELPERS
    // =========================================================

    public static string FormatDuration(TimeSpan duration)
    {
        return
            $"{(int)duration.TotalHours:00}:" +
            $"{duration.Minutes:00}:" +
            $"{duration.Seconds:00}";
    }
    
    private static bool IsExecutionEndingState(string value)
    {
        return EndingExecutionStates.Contains(value);
    }
}