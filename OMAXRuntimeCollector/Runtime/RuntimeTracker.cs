using OMAXRuntimeCollector.Runtime.Writer;

namespace OMAXRuntimeCollector.Runtime;

/// <summary>
/// Tracks machine execution states and calculates runtime
/// for the morning and afternoon accounting periods.
/// </summary>
public class RuntimeTracker
{
    private readonly IReadOnlyList<IRuntimeWriter> _runtimeWriters;
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


    /// <summary>
    /// Creates a new runtime tracker.
    /// </summary>
    /// <param name="runtimeWriters">
    /// Writers used to persist calculated runtime.
    /// </param>
    /// <param name="logger">
    /// Logger used to record runtime tracking events and errors.
    /// </param>
    /// <param name="runtimeCalculator">
    /// Calculator used to determine how execution time is divided
    /// between the morning and afternoon accounting periods.
    /// </param>
    public RuntimeTracker(IReadOnlyList<IRuntimeWriter> runtimeWriters, AppLogger logger, RuntimeCalculator runtimeCalculator)
    {
        _runtimeWriters = runtimeWriters;
        _logger = logger;
        _runtimeCalculator = runtimeCalculator;
    }


    /// <summary>
    /// Processes a line received from the OMAX machine's MTConnect stream.
    /// Parses the timestamp and execution state, then updates the current
    /// runtime tracking state accordingly.
    /// </summary>
    /// <param name="line">
    /// A pipe-delimited MTConnect data line containing a UTC timestamp
    /// and machine data, including the execution state when available.
    /// </param>
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
            _logger.Warning($"Invalid timestamp received: {fields[0]}");
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


    /// <summary>
    /// Processes a runtime accounting boundary at 14:00 or midnight.
    /// At 14:00, the current morning runtime is saved and an active
    /// execution is split between the morning and afternoon periods.
    /// At midnight, the current afternoon runtime is saved and the
    /// accounting counters are reset for the new day.
    /// </summary>
    /// <param name="boundary">
    /// The local date and time of the accounting boundary.
    /// </param>
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
            
            _logger.Info("Machine is still active. " + "Continuing into afternoon period.");
        }


        _logger.Info($"Morning runtime: " + $"{FormatDuration(_morningRuntime)}");

        foreach (IRuntimeWriter writer in _runtimeWriters)
            SaveRuntime(writer, () => writer.SaveMorningRuntime(boundary.Date, _morningRuntime));
        
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


        _logger.Info($"Afternoon runtime: " + $"{FormatDuration(_afternoonRuntime)}");

        foreach (var writer in _runtimeWriters)
            SaveRuntime(writer, () => writer.SaveAfternoonRuntime(previousDay, _afternoonRuntime));

        _logger.Info("Afternoon runtime saved.");


        // -----------------------------------------------------
        // Reset counters for the new accounting day.
        // -----------------------------------------------------

        _morningRuntime = TimeSpan.Zero;
        _afternoonRuntime = TimeSpan.Zero;


        _logger.Info($"Starting new runtime day: " + $"{boundary:yyyy-MM-dd}");
    }


    /// <summary>
    /// Handles loss of the connection to the OMAX machine.
    /// Any currently active execution is discarded because the machine's
    /// state during the disconnected period cannot be reliably determined.
    /// </summary>
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


    /// <summary>
    /// Gets the runtime accumulated for the current accounting day.
    /// </summary>
    /// <returns>
    /// A tuple containing the accumulated morning and afternoon runtime.
    /// </returns>
    public (TimeSpan Morning, TimeSpan Afternoon) GetCurrentRuntime()
    {
        lock (_stateLock)
        {
            return (_morningRuntime, _afternoonRuntime);
        }
    }


    #region HELPERS

        /// <summary>
        /// Formats a runtime duration as hours, minutes, and seconds.
        /// </summary>
        /// <param name="duration">
        /// The duration to format.
        /// </param>
        /// <returns>
        /// The duration formatted as <c>HH:MM:SS</c>.
        /// </returns>
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
        
        private void SaveRuntime(IRuntimeWriter writer, Action saveAction)
        {
            try
            {
                saveAction();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save runtime using {writer.GetType().Name}: {ex.Message}");
                if (writer.IsCritical)
                    throw;
            }
        }

    #endregion
}