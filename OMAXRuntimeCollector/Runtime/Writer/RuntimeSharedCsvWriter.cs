namespace OMAXRuntimeCollector.Runtime.Writer;

public class RuntimeSharedCsvWriter : RuntimeCsvWriterBase
{
    private const string LockFileSuffix = ".lock";
    private const int LockTimeoutMilliseconds = 10_000;
    private const int LockRetryDelayMilliseconds = 100;
    
    private readonly int _lockTimeoutMilliseconds;
    private readonly int _lockRetryDelayMilliseconds;

    public override bool IsCritical => false;
    
    public RuntimeSharedCsvWriter(string filePath, AppLogger logger, string machineId,
        int lockTimeoutMilliseconds = LockTimeoutMilliseconds,
        int lockRetryDelayMilliseconds = LockRetryDelayMilliseconds)
        : base(filePath, logger, machineId)
    {
        _lockTimeoutMilliseconds = lockTimeoutMilliseconds;
        _lockRetryDelayMilliseconds = lockRetryDelayMilliseconds;
    }

    protected override void UpdateCsvRow(DateTime date, TimeSpan? morningRuntime, TimeSpan? afternoonRuntime)
    {
        string lockFilePath = _filePath + LockFileSuffix;
        using FileStream lockStream = AcquireLock(lockFilePath);
        base.UpdateCsvRow(date, morningRuntime, afternoonRuntime);
    }
    
    

    /// <summary>
    /// Attempts to acquire an exclusive lock on the lock file.
    /// Retries until the lock is acquired or the configured timeout is reached.
    /// </summary>
    /// <param name="lockFilePath">
    /// The path of the lock file used to coordinate access to the shared CSV.
    /// </param>
    /// <returns>
    /// A <see cref="FileStream"/> that holds the lock for the lifetime of the stream.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown if the lock cannot be acquired before the configured timeout.
    /// </exception>
    private FileStream AcquireLock(string lockFilePath)
    {
        DateTime timeout = DateTime.UtcNow.AddMilliseconds(_lockTimeoutMilliseconds);

        while (true)
        {
            try
            {
                return new FileStream (lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= timeout)
                {
                    _logger.Error($"Timed out waiting for CSV lock: {lockFilePath}");
                    throw;
                }

                Thread.Sleep(_lockRetryDelayMilliseconds);
            }
        }
    }
}