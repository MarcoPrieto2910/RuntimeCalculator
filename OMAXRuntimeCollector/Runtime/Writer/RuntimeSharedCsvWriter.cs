namespace OMAXRuntimeCollector.Runtime.Writer;

/// <summary>
/// Writes runtime data to the shared CSV file used by multiple OMAX machines.
/// Uses a sidecar lock file to coordinate access between machines writing
/// to the same file.
/// </summary>
public class RuntimeSharedCsvWriter : RuntimeCsvWriterBase
{
    private const string LockFileSuffix = ".lock";
    private const int LockTimeoutMilliseconds = 10_000;
    private const int LockRetryDelayMilliseconds = 100;
    
    private readonly int _lockTimeoutMilliseconds;
    private readonly int _lockRetryDelayMilliseconds;

    /// <summary>
    /// Gets whether a failure in the shared writer should be treated as critical.
    /// </summary>
    public override bool IsCritical => false;
    
    
    /// <summary>
    /// Initializes a new shared CSV writer.
    /// </summary>
    /// <param name="filePath">The path of the shared runtime CSV file.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="machineId">The identifier of the machine whose runtime is being recorded.</param>
    /// <param name="lockTimeoutMilliseconds">
    /// Maximum time to wait for the shared CSV lock before failing.
    /// </param>
    /// <param name="lockRetryDelayMilliseconds">
    /// Delay between attempts to acquire the shared CSV lock.
    /// </param>
    public RuntimeSharedCsvWriter(string filePath, AppLogger logger, string machineId, int lockTimeoutMilliseconds = LockTimeoutMilliseconds, int lockRetryDelayMilliseconds = LockRetryDelayMilliseconds)
        : base(filePath, logger, machineId)
    {
        _lockTimeoutMilliseconds = lockTimeoutMilliseconds;
        _lockRetryDelayMilliseconds = lockRetryDelayMilliseconds;
    }

    /// <summary>
    /// Acquires the shared CSV lock before updating the file.
    /// The lock remains held until the CSV update is complete.
    /// </summary>
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