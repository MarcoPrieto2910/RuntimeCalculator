namespace OMAXRuntimeCollector.Runtime.Writer;

public interface IRuntimeWriter
{
    void SaveMorningRuntime(DateTime date, TimeSpan runtime);
    void SaveAfternoonRuntime(DateTime date, TimeSpan runtime);
}