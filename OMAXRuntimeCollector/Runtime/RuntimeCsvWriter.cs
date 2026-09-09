namespace OMAXRuntimeCollector.Runtime;

public class RuntimeCsvWriter
{
    private readonly string _filePath;
    private readonly string _machineId;
    private readonly AppLogger _logger;
    private const string Header = "MachineId,Date,MorningRuntime,AfternoonRuntime";


    public RuntimeCsvWriter(string filePath, AppLogger logger, string machineId)
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


    public void SaveMorningRuntime(DateTime date, TimeSpan runtime)
    {
        UpdateCsvRow(date, runtime, null);
    }


    public void SaveAfternoonRuntime(DateTime date, TimeSpan runtime)
    {
        UpdateCsvRow(date, null, runtime);
    }


    private void UpdateCsvRow(DateTime date, TimeSpan? morningRuntime, TimeSpan? afternoonRuntime)
    {
        string dateString = date.ToString("yyyy-MM-dd");

        List<string> lines;

        // -----------------------------------------------------
        // Load existing CSV.
        // -----------------------------------------------------

        if (File.Exists(_filePath))
        {
            lines = File.ReadAllLines(_filePath).ToList();
            if (lines.Count == 0)
            {
                lines.Add(Header);
            }
        }
        else
        {
            lines = new List<string>
            {
                Header
            };
        }


        // -----------------------------------------------------
        // Find existing row.
        // -----------------------------------------------------

        int rowIndex = -1;

        for (int i = 1; i < lines.Count; i++)
        {
            string[] fields =
                lines[i].Split(',');

            if (fields.Length > 1 && fields[0] == _machineId && fields[1] == dateString)
            {
                rowIndex = i;
                break;
            }
        }


        // -----------------------------------------------------
        // Update existing row.
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // Create new row.
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // Write file.
        // -----------------------------------------------------

        File.WriteAllLines(_filePath, lines);
        _logger.Info($"Runtime CSV updated: {_filePath}");
    }
}