namespace OMAXRuntimeCollector.Runtime.Writer;

public class RuntimeSharedCsvWriter : RuntimeCsvWriterBase
{
    private const string LockFileSuffix = ".lock";
    private const int LockTimeoutMilliseconds = 10_000;
    private const int LockRetryDelayMilliseconds = 100;
    
    public RuntimeSharedCsvWriter(string filePath, AppLogger logger, string machineId) : base(filePath, logger, machineId) { }

    protected override void UpdateCsvRow(DateTime date, TimeSpan? morningRuntime, TimeSpan? afternoonRuntime)
    {
        string lockFilePath = _filePath + LockFileSuffix;
        DateTime timeout = DateTime.UtcNow.AddMilliseconds(LockTimeoutMilliseconds);

        while (true)
        {
            try
            {
                using FileStream lockStream = new(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                base.UpdateCsvRow(date, morningRuntime, afternoonRuntime);
                return;
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= timeout)
                {
                    _logger.Error($"Timed out waiting for CSV lock: {lockFilePath}");
                    throw;
                }

                Thread.Sleep(LockRetryDelayMilliseconds);
            }
        }
    }
}