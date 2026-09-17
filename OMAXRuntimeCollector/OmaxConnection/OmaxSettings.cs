namespace OMAXRuntimeCollector.OmaxConnection;


/// <summary>
/// Represents the complete configuration for the OMAX Runtime Collector.
/// </summary>
public class OmaxSettings
{
    /// <summary>
    /// Gets or sets the unique identifier of the OMAX machine being monitored.
    /// This value is included in the shared runtime CSV to distinguish
    /// runtime data from different machines.
    /// </summary>
    public required string MachineId { get; set; }

    /// <summary>
    /// Gets or sets the OMAX MTConnect connection settings.
    /// </summary>
    public OmaxConnectionSettings Omax { get; set; } = new();

    /// <summary>
    /// Gets or sets the runtime CSV and log file storage settings.
    /// </summary>
    public StorageSettings Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the application should run in test mode.
    /// </summary>
    public bool TestMode { get; set; }
}


/// <summary>
/// Represents the connection settings used to connect to the OMAX computer's
/// MTConnect endpoint.
/// </summary>
public class OmaxConnectionSettings
{
    /// <summary>
    /// Gets or sets the hostname or IP address of the OMAX computer.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the TCP port used by the OMAX MTConnect server.
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the number of seconds to wait before attempting to
    /// reconnect after an OMAX connection is lost.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;
}



/// <summary>
/// Represents the file storage locations used by the collector.
/// </summary>
public class StorageSettings
{
    /// <summary>
    /// Gets or sets the path of the machine-local runtime CSV.
    /// Environment variables in the path are expanded before use.
    /// </summary>
    public string LocalCsvPath { get; set; } = "%ProgramData%\\OMAXRuntimeCollector\\runtime.csv";

    /// <summary>
    /// Gets or sets the path of the shared runtime CSV used by all OMAX collectors.
    /// </summary>
    public string SharedCsvPath { get; set; } = "P:\\OMAXRuntimeCollector\\runtime.csv";

    /// <summary>
    /// Gets or sets the path of the collector log file.
    /// Environment variables in the path are expanded before use.
    /// </summary>
    public string LogPath { get; set; } = "%ProgramData%\\OMAXRuntimeCollector\\collector.log";
}