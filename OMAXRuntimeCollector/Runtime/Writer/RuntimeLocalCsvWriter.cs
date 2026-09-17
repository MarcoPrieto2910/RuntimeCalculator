namespace OMAXRuntimeCollector.Runtime.Writer;

/// <summary>
/// Writes runtime data to the machine's local CSV file.
/// </summary>
public class RuntimeLocalCsvWriter : RuntimeCsvWriterBase
{
    /// <summary>
    /// Gets whether a failure in the local writer should be treated as critical.
    /// </summary>
    public override bool IsCritical => true;

    
    /// <summary>
    /// Initializes a new local CSV writer.
    /// </summary>
    /// <param name="filePath">The path of the local runtime CSV file.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="machineId">The identifier of the machine whose runtime is being recorded.</param>
    public RuntimeLocalCsvWriter(string filePath, AppLogger logger, string machineId) : base(filePath, logger, machineId) { }
}