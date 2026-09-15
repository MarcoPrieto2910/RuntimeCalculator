using OMAXRuntimeCollector.OmaxConnection;

namespace OMAXRuntimeCollector.Tests;

public class ConfigurationValidatorTests
{
    [Fact]
    public void Validate_ValidConfiguration_ReturnsNoErrors()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings
            {
                Host = "localhost",
                Port = 5000,
                ReconnectDelaySeconds = 5
            },
            Storage = new StorageSettings
            {
                LocalCsvPath = @"C:\OMAXRuntimeCollector\runtime.csv",
                LogPath = @"C:\OMAXRuntimeCollector\collector.log"
            }
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Empty(errors);
    }
    
    // =========================================================
    // INVALID MACHINE ID
    // =========================================================

    [Fact]
    public void Validate_EmptyMachineId_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "",
            Omax = new OmaxConnectionSettings(),
            Storage = new StorageSettings()
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("MachineId cannot be empty.", errors);
    }


    // =========================================================
    // INVALID HOST
    // =========================================================

    [Fact]
    public void Validate_EmptyHost_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings
            {
                Host = ""
            },
            Storage = new StorageSettings()
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Omax.Host cannot be empty.", errors);
    }


    // =========================================================
    // INVALID PORT
    // =========================================================

    [Fact]
    public void Validate_InvalidPort_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings
            {
                Port = 0
            },
            Storage = new StorageSettings()
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Omax.Port must be between 1 and 65535.", errors);
    }


    // =========================================================
    // INVALID RECONNECT DELAY
    // =========================================================

    [Fact]
    public void Validate_InvalidReconnectDelay_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings
            {
                ReconnectDelaySeconds = 0
            },
            Storage = new StorageSettings()
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Omax.ReconnectDelaySeconds must be greater than 0.", errors);
    }


    // =========================================================
    // INVALID CSV PATH
    // =========================================================

    [Fact]
    public void Validate_EmptyCsvPath_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings(),
            Storage = new StorageSettings
            {
                LocalCsvPath = ""
            }
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Storage.LocalCsvPath cannot be empty.", errors);
    }
    
    [Fact]
    public void Validate_EmptySharedCsvPath_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings(),
            Storage = new StorageSettings
            {
                SharedCsvPath = ""
            }
        };
        
        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Storage.SharedCsvPath cannot be empty.", errors);
    }


    // =========================================================
    // INVALID LOG PATH
    // =========================================================

    [Fact]
    public void Validate_EmptyLogPath_ReturnsError()
    {
        var settings = new OmaxSettings
        {
            MachineId = "OMAX-01",
            Omax = new OmaxConnectionSettings(),
            Storage = new StorageSettings
            {
                LogPath = ""
            }
        };

        List<string> errors = ConfigurationValidator.Validate(settings);
        Assert.Contains("Storage.LogPath cannot be empty.", errors);
    }
    
    // =========================================================
    // MULTIPLE INVALID SETTINGS
    // =========================================================

    [Fact]
    public void Validate_MultipleInvalidSettings_ReturnsAllErrors()
    {
        var settings = new OmaxSettings
        {
            MachineId = "",
            Omax = new OmaxConnectionSettings
            {
                Host = "",
                Port = 0,
                ReconnectDelaySeconds = 0
            },
            Storage = new StorageSettings
            {
                LocalCsvPath = "",
                SharedCsvPath = "",
                LogPath = ""
            }
        };

        List<string> errors = ConfigurationValidator.Validate(settings);

        Assert.Equal(7, errors.Count);
        Assert.Contains("MachineId cannot be empty.", errors);
        Assert.Contains("Omax.Host cannot be empty.", errors);
        Assert.Contains("Omax.Port must be between 1 and 65535.", errors);
        Assert.Contains("Omax.ReconnectDelaySeconds must be greater than 0.", errors);
        Assert.Contains("Storage.LocalCsvPath cannot be empty.", errors);
        Assert.Contains("Storage.SharedCsvPath cannot be empty.", errors);
        Assert.Contains("Storage.LogPath cannot be empty.", errors);
    }
}