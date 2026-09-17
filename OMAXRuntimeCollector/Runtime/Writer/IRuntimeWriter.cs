namespace OMAXRuntimeCollector.Runtime.Writer;

public interface IRuntimeWriter
{
    /// <summary>
    /// Gets whether a failure in this writer should be treated as
    /// critical and propagated to the caller.
    /// </summary>
    bool IsCritical { get; }

    /// <summary>
    /// Saves the specified morning runtime for a machine and date.
    /// </summary>
    /// <param name="date">The date the runtime belongs to.</param>
    /// <param name="runtime">The accumulated morning runtime.</param>
    void SaveMorningRuntime(DateTime date, TimeSpan runtime);

    /// <summary>
    /// Saves the specified afternoon runtime for a machine and date.
    /// </summary>
    /// <param name="date">The date the runtime belongs to.</param>
    /// <param name="runtime">The accumulated afternoon runtime.</param>
    void SaveAfternoonRuntime(DateTime date, TimeSpan runtime);
}