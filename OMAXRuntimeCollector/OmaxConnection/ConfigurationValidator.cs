namespace OMAXRuntimeCollector.OmaxConnection;

/// <summary>
/// Validates the application configuration and returns any configuration errors.
/// </summary>
public static class ConfigurationValidator
{
    
    /// <summary>
    /// Validates the supplied OMAX Runtime Collector configuration.
    /// </summary>
    /// <param name="settings">
    /// The configuration settings to validate.
    /// </param>
    /// <returns>
    /// A list containing one message for each validation error.
    /// An empty list indicates that the configuration is valid.
    /// </returns>
    public static List<string> Validate(OmaxSettings settings)
    {
        List<string> errors = new();
        
        if (string.IsNullOrWhiteSpace(settings.MachineId))
        {
            errors.Add("MachineId cannot be empty.");
        }
        
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