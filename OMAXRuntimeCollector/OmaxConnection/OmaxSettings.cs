namespace OMAXRuntimeCollector.OmaxConnection;

public class OmaxSettings
{
    public OmaxConnectionSettings Omax { get; set; } = new();
    public StorageSettings Storage { get; set; } = new();
    public bool TestMode { get; set; }
}


public class OmaxConnectionSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5000;
    public int ReconnectDelaySeconds { get; set; } = 5;
}


public class StorageSettings
{
    public string CsvPath { get; set; } = "%ProgramData%\\OMAXRuntimeCollector\\runtime.csv";
    public string LogPath { get; set; } = "%ProgramData%\\OMAXRuntimeCollector\\collector.log";
}