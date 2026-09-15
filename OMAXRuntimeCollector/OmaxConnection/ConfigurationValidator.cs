namespace OMAXRuntimeCollector.OmaxConnection;

public static class ConfigurationValidator
{
    public static List<string> Validate(OmaxSettings settings)
    {
        List<string> errors = new();

        // -----------------------------------------------------
        // Machine ID
        // -----------------------------------------------------

        if (string.IsNullOrWhiteSpace(settings.MachineId))
        {
            errors.Add("MachineId cannot be empty.");
        }


        // -----------------------------------------------------
        // OMAX connection
        // -----------------------------------------------------

        if (string.IsNullOrWhiteSpace(settings.Omax.Host))
        {
            errors.Add("Omax.Host cannot be empty.");
        }

        if (settings.Omax.Port < 1 || settings.Omax.Port > 65535)
        {
            errors.Add("Omax.Port must be between 1 and 65535.");
        }

        if (settings.Omax.ReconnectDelaySeconds <= 0)
        {
            errors.Add("Omax.ReconnectDelaySeconds must be greater than 0.");
        }


        // -----------------------------------------------------
        // Storage
        // -----------------------------------------------------

        if (string.IsNullOrWhiteSpace(settings.Storage.LocalCsvPath))
        {
            errors.Add("Storage.LocalCsvPath cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(settings.Storage.SharedCsvPath))
        {
            errors.Add("Storage.SharedCsvPath cannot be empty.");
        }
        
        if (string.IsNullOrWhiteSpace(settings.Storage.LogPath))
        {
            errors.Add("Storage.LogPath cannot be empty.");
        }


        return errors;
    }
}