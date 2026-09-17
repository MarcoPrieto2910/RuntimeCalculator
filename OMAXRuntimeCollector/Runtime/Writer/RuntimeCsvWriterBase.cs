namespace OMAXRuntimeCollector.Runtime.Writer;

/// <summary>
/// Provides the common CSV persistence logic shared by runtime writers.
/// Concrete writers can customize how CSV updates are performed, such as
/// adding synchronization for a shared file.
/// </summary>
public abstract class RuntimeCsvWriterBase : IRuntimeWriter
{
    protected readonly string _filePath;
    protected readonly string _machineId;
    protected readonly AppLogger _logger;
    protected const string Header = "MachineId,Date,MorningRuntime,AfternoonRuntime";
    
    /// <summary>
    /// Gets whether a failure in this writer should be treated as
    /// critical and propagated to the caller.
    /// </summary>
    public abstract bool IsCritical { get; }
    
    
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeCsvWriterBase"/> class.
    /// </summary>
    /// <param name="filePath">
    /// The path of the CSV file to write.
    /// Environment variables in the path are expanded before use.
    /// </param>
    /// <param name="logger">The application logger.</param>
    /// <param name="machineId">
    /// The identifier of the machine whose runtime is being recorded.
    /// </param>
    protected RuntimeCsvWriterBase(string filePath, AppLogger logger, string machineId)
    {
        _filePath = Environment.ExpandEnvironmentVariables(filePath);
        _logger = logger;
        _machineId = machineId;

        string? directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }


    /// <summary>
    /// Saves or updates the morning runtime for the specified date.
    /// </summary>
    /// <param name="date">The date the runtime belongs to.</param>
    /// <param name="runtime">The accumulated morning runtime.</param>
    public void SaveMorningRuntime(DateTime date, TimeSpan runtime)
    {
        UpdateCsvRow(date, runtime, null);
    }

    
    /// <summary>
    /// Saves or updates the afternoon runtime for the specified date.
    /// </summary>
    /// <param name="date">The date the runtime belongs to.</param>
    /// <param name="runtime">The accumulated afternoon runtime.</param>
    public void SaveAfternoonRuntime(DateTime date, TimeSpan runtime)
    {
        UpdateCsvRow(date, null, runtime);
    }

    
    /// <summary>
    /// Updates the CSV row corresponding to the configured machine and date.
    /// If the row does not exist, a new row is created.
    /// </summary>
    /// <param name="date">The date the runtime belongs to.</param>
    /// <param name="morningRuntime">
    /// The morning runtime to save, or <see langword="null"/> when only
    /// the afternoon runtime should be updated.
    /// </param>
    /// <param name="afternoonRuntime">
    /// The afternoon runtime to save, or <see langword="null"/> when only
    /// the morning runtime should be updated.
    /// </param>
    protected virtual void UpdateCsvRow(DateTime date, TimeSpan? morningRuntime, TimeSpan? afternoonRuntime)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        List<string> lines;

        if (File.Exists(_filePath))
        {
            lines = File.ReadAllLines(_filePath).ToList();
            if (lines.Count == 0)
                lines.Add(Header);
        }
        else
        {
            lines = new List<string> { Header };
        }

        int rowIndex = -1;
        for (int i = 1; i < lines.Count; i++)
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length > 1 && fields[0] == _machineId && fields[1] == dateString)
            {
                rowIndex = i;
                break;
            }
        }
        
        if (rowIndex >= 0)
        {
            string[] fields = lines[rowIndex].Split(',');

            string existingMorning =
                fields.Length > 2
                    ? fields[2]
                    : "00:00:00";

            string existingAfternoon =
                fields.Length > 3
                    ? fields[3]
                    : "00:00:00";


            if (morningRuntime.HasValue)
            {
                existingMorning =
                    RuntimeTracker.FormatDuration(
                        morningRuntime.Value);
            }


            if (afternoonRuntime.HasValue)
            {
                existingAfternoon =
                    RuntimeTracker.FormatDuration(
                        afternoonRuntime.Value);
            }


            lines[rowIndex] = $"{_machineId}," +
                              $"{dateString}," +
                              $"{existingMorning}," +
                              $"{existingAfternoon}";
        }
        else
        {
            string morning =
                morningRuntime.HasValue
                    ? RuntimeTracker.FormatDuration(
                        morningRuntime.Value)
                    : "00:00:00";


            string afternoon =
                afternoonRuntime.HasValue
                    ? RuntimeTracker.FormatDuration(
                        afternoonRuntime.Value)
                    : "00:00:00";


            lines.Add($"{_machineId}," +
                      $"{dateString}," +
                      $"{morning}," +
                      $"{afternoon}");
        }
        
        File.WriteAllLines(_filePath, lines);
        _logger.Info($"Runtime CSV updated: {_filePath}");
    }
}