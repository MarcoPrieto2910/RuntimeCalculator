namespace OMAXRuntimeCollector.Runtime.Writer;

public interface IRuntimeWriter
{
    /// <summary>
    /// Gets whether a failure in this writer should be treated as
    /// critical and propagated to the caller.
    /// </summary>
    bool IsCritical { get; }
    
    void SaveMorningRuntime(DateTime date, TimeSpan runtime);
    void SaveAfternoonRuntime(DateTime date, TimeSpan runtime);
}