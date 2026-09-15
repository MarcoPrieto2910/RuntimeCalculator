namespace OMAXRuntimeCollector.Runtime.Writer;

public class RuntimeLocalCsvWriter : RuntimeCsvWriterBase
{
    public RuntimeLocalCsvWriter(string filePath, AppLogger logger, string machineId) : base(filePath, logger, machineId)
    {
    }
}