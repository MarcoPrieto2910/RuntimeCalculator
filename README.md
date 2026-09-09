# OMAX Runtime Collector

A C# application for monitoring the runtime of an OMAX waterjet cutting machine through its machine log stream.

The application connects to an OMAX computer, listens for execution state changes, calculates how long the machine has been running during defined work periods, and stores the results in a CSV file.

The collector can run continuously as a **Windows Service**, automatically start with Windows, reconnect when the OMAX endpoint becomes unavailable, and recover automatically if the collector process fails.

## Overview

The **OMAX Runtime Collector** was developed to track machine usage without requiring a third-party monitoring application.

The collector listens to the OMAX log stream and identifies when a cutting job starts and stops. From these events, it calculates the machine's active runtime and separates it into two daily periods:

* **Morning:** 05:00 → 14:00
* **Afternoon:** 14:00 → 00:00

Runtime data is periodically written to a CSV file for later analysis.

The application is designed to run continuously in the background as a Windows Service. It automatically reconnects if the OMAX connection is temporarily unavailable and can be configured to automatically restart if the collector process fails.

## Features

* Connects directly to the OMAX machine's TCP endpoint and continuously reads its machine log stream.
* Monitors machine execution state.
* Detects `ACTIVE` and ending execution events.
* Calculates runtime without relying on a dedicated runtime value from the machine.
* Separates runtime into morning and afternoon periods.
* Handles executions crossing:

    * 05:00
    * 14:00
    * Midnight
* Automatically saves runtime data to CSV.
* Includes a unique machine identifier in the runtime data.
* Logs application activity and connection problems.
* Automatically attempts to reconnect when the OMAX endpoint becomes unavailable.
* Validates configuration during application startup.
* Configurable connection and storage settings through `appsettings.json`.
* Runs as a Windows Service using the .NET Generic Host and `BackgroundService`.
* Can automatically start when Windows starts.
* Can automatically restart after an unexpected process failure.
* Includes application version information in startup diagnostics.
* Includes automated unit tests for runtime calculation, CSV writing, execution state tracking, configuration validation, and time boundaries.
* Includes a fake OMAX server for local development and testing.
* Includes PowerShell scripts for publishing, installing, and uninstalling the Windows Service.

## Repository Structure

```text
RuntimeCollector/
│
├── OMAXRuntimeCollector/
│   ├── OmaxConnection/
│   │   ├── ConfigurationValidator.cs
│   │   ├── OmaxClient.cs
│   │   └── OmaxSettings.cs
│   │
│   ├── Runtime/
│   │   ├── RuntimeCalculator.cs
│   │   ├── RuntimeCsvWriter.cs
│   │   └── RuntimeTracker.cs
│   │
│   ├── scripts/
│   │   ├── publish.ps1
│   │   ├── install-service.ps1
│   │   └── uninstall-service.ps1
│   │
│   ├── AppLogger.cs
│   ├── Program.cs
│   ├── RuntimeCollectorWorker.cs
│   ├── appsettings.json
│   └── OMAXRuntimeCollector.csproj
│
├── OMAXRuntimeCollector.Tests/
│   ├── ConfigurationValidatorTests.cs
│   ├── RuntimeCalculatorTests.cs
│   ├── RuntimeCsvWriterTests.cs
│   ├── RuntimeTrackerBoundaryTests.cs
│   ├── RuntimeTrackerExecutionStateTests.cs
│   └── OMAXRuntimeCollector.Tests.csproj
│
└── FakeOmax/
    └── FakeOmax/
        ├── Program.cs
        ├── appsettings.json
        ├── stream-test.txt
        ├── stream-test2.txt
        └── FakeOmax.csproj
```

### Projects

#### OMAXRuntimeCollector

The main application.

It connects to the OMAX endpoint, processes machine events, calculates runtime, and writes the results to CSV.

The application is hosted using the .NET Generic Host and runs its collection logic through `RuntimeCollectorWorker`, which derives from `BackgroundService`.

During startup, the application loads and validates its configuration before attempting to connect to the OMAX endpoint.

When installed as a Windows Service, the collector runs in the background without requiring a user to manually launch the application.

#### OMAXRuntimeCollector.Tests

Contains the automated unit tests for the application.

The tests cover:

* Runtime calculation logic
* CSV output
* Execution state transitions
* Time-boundary behavior
* Configuration validation

#### FakeOmax

A lightweight local server used to simulate the OMAX endpoint during development.

Instead of connecting to a real machine, it reads test data from text files and exposes it through a TCP endpoint.

This makes it possible to test the collector without requiring access to the physical machine.

## Requirements

* Windows
* .NET 9 SDK
* Access to an OMAX machine running the required TCP endpoint

For development and testing, an OMAX machine is not required because the `FakeOmax` project can simulate the machine connection.

For deployment to the current OMAX machine, the application is published as a **self-contained 32-bit (`win-x86`) executable**.

## Configuration

The collector uses `appsettings.json` for its configuration.

Example:

```json
{
  "MachineId": "OMAX-01",
  "Omax": {
    "Host": "localhost",
    "Port": 5000,
    "ReconnectDelaySeconds": 5
  },
  "Storage": {
    "CsvPath": "%ProgramData%\\OMAXRuntimeCollector\\runtime.csv",
    "LogPath": "%ProgramData%\\OMAXRuntimeCollector\\collector.log"
  },
  "TestMode": false
}
```

### Machine ID

`MachineId` identifies the OMAX machine associated with the collector.

Each machine should have its own unique identifier.

Example:

```json
"MachineId": "OMAX-01"
```

The machine ID is included in the CSV output so that runtime data from multiple machines can be distinguished when stored together.

### OMAX settings

| Setting                 | Description                                 |
|-------------------------|---------------------------------------------|
| `Host`                  | Hostname or IP address of the OMAX computer |
| `Port`                  | Port used by the OMAX TCP endpoint          |
| `ReconnectDelaySeconds` | Delay before attempting to reconnect        |

### Storage settings

| Setting   | Description                           |
|-----------|---------------------------------------|
| `CsvPath` | Location where runtime data is stored |
| `LogPath` | Location of the application log       |

Environment variables such as `%ProgramData%` are expanded automatically.

### TestMode

`TestMode` controls whether application log messages are also written to the console.

```json
"TestMode": true
```

is useful during development and local testing.

For Windows Service deployment, it should normally be set to:

```json
"TestMode": false
```

The application log is still written to the configured log file.

### Configuration Validation

The application validates its configuration during startup before attempting to connect to the OMAX endpoint.

The following settings are validated:

* `MachineId` must not be empty.
* `Omax.Host` must not be empty.
* `Omax.Port` must be between 1 and 65535.
* `Omax.ReconnectDelaySeconds` must be greater than 0.
* `Storage.CsvPath` must not be empty.
* `Storage.LogPath` must not be empty.

If one or more configuration errors are detected, the application reports all detected errors and stops before attempting to connect to the OMAX endpoint.

## Running the Application

For development and testing, the application can be run directly from the project directory:

```powershell
dotnet run
```

The application will load and validate its configuration, connect to the configured OMAX endpoint, and begin monitoring machine activity.

To stop the application when running interactively, press:

```text
Ctrl+C
```

When deployed as a Windows Service, the application is instead started and stopped by Windows.

## Running with the Fake OMAX Server

The `FakeOmax` project can be used to simulate the machine during development.

Start the fake server first:

```powershell
dotnet run --project FakeOmax/FakeOmax
```

Then start the collector:

```powershell
dotnet run --project OMAXRuntimeCollector
```

The fake server reads one of the provided test streams and exposes the simulated events through its configured TCP endpoint.

This allows the runtime tracking behavior to be tested without connecting to the physical machine.

## Testing

Run all automated tests from the repository root:

```powershell
dotnet test
```

The test suite verifies:

* Runtime calculation
* Multiple executions
* Executions crossing 05:00
* Executions crossing 14:00
* Executions crossing midnight
* Morning runtime storage
* Afternoon runtime storage
* CSV creation and updates
* Multiple machine CSV data
* Execution state transitions
* Runtime processing at time boundaries
* Configuration validation

All automated tests should pass before publishing a new version of the application.

## Building the Executable

The application can be published as a self-contained executable so that the target computer does not need the .NET runtime installed.

For the current OMAX environment, the application must be published for **32-bit Windows (`win-x86`)**:

```powershell
dotnet publish -c Release -r win-x86 --self-contained true
```

The published files are placed under:

```text
OMAXRuntimeCollector/bin/Release/net9.0/win-x86/publish/
```

The published directory contains the executable, configuration file, and required runtime files.

Before installing the application as a Windows Service, copy the published files to a permanent directory on the target computer.

For example:

```text
C:\OMAXRuntimeCollector
```

## PowerShell Scripts

The project includes PowerShell scripts to simplify publishing and Windows Service deployment.

The scripts are located in:

```text
OMAXRuntimeCollector/scripts/
```

They are:

| Script                  | Purpose                                                              |
|-------------------------|----------------------------------------------------------------------|
| `publish.ps1`           | Publishes the application as a self-contained `win-x86` executable   |
| `install-service.ps1`   | Creates, configures, and starts the Windows Service                  |
| `uninstall-service.ps1` | Stops and removes the Windows Service and its installation directory |

The scripts are designed so that the deployment location is provided when needed rather than being tied to a specific development machine or Rider publish directory.

### Publish

From the `scripts` directory, run:

```powershell
.\publish.ps1
```

The script publishes the application using:

```powershell
dotnet publish -c Release -r win-x86 --self-contained true
```

The resulting files are placed in:

```text
OMAXRuntimeCollector/bin/Release/net9.0/win-x86/publish/
```

The published files can then be copied to the desired installation directory on the target computer.

For example:

```text
C:\OMAXRuntimeCollector
```

### Install the Windows Service

After copying the published files to the target installation directory, open **PowerShell as Administrator**.

Run:

```powershell
.\install-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

The installation script:

1. Verifies that it is running with administrator privileges.
2. Verifies that the installation directory exists.
3. Verifies that `OMAXRuntimeCollector.exe` exists.
4. Stops and removes an existing installation of the service if necessary.
5. Creates the Windows Service.
6. Configures automatic startup.
7. Configures Windows Service recovery.
8. Starts the service.
9. Displays the resulting service status.

The service is created with:

* **Service name:** `OMAXRuntimeCollector`
* **Display name:** `OMAX Runtime Collector`
* **Startup type:** Automatic

The script also configures three automatic restart attempts with a five-second delay:

```text
First failure  → restart after 5 seconds
Second failure → restart after 5 seconds
Third failure  → restart after 5 seconds
```

The failure count is reset after 24 hours.

### Uninstall the Windows Service

To remove the service and its installation files, open **PowerShell as Administrator** and run:

```powershell
.\uninstall-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

The uninstall script:

1. Verifies administrator privileges.
2. Stops the service if it is running.
3. Removes the Windows Service.
4. Removes the specified installation directory.

The script does not affect the project's source files or published files stored elsewhere.

### Script Requirements

The installation and uninstallation scripts require an **elevated PowerShell session** because creating, removing, and configuring Windows Services requires administrator privileges.

The publish script does not require administrator privileges when publishing from a normal development environment.

## Windows Service Deployment

The collector can be installed as a Windows Service so that it runs automatically in the background and starts with Windows.

The recommended deployment process is:

### 1. Run the tests

From the repository root:

```powershell
dotnet test
```

Make sure all tests pass before publishing.

### 2. Publish the application

From the `OMAXRuntimeCollector/scripts` directory:

```powershell
.\publish.ps1
```

### 3. Copy the published files

Copy the contents of:

```text
OMAXRuntimeCollector/bin/Release/net9.0/win-x86/publish/
```

to the target installation directory.

For example:

```text
C:\OMAXRuntimeCollector
```

Make sure the directory contains at least:

```text
OMAXRuntimeCollector.exe
appsettings.json
```

along with the other published application files.

### 4. Configure the application

Before starting the service, verify `appsettings.json`.

For a production installation, `TestMode` should normally be:

```json
"TestMode": false
```

Also verify that the configured `MachineId`, OMAX host, port, CSV path, and log path are correct for the target environment.

Each OMAX machine should have a unique `MachineId`.

### 5. Install the service

Open **PowerShell as Administrator** and run:

```powershell
.\install-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

The script creates and starts the service automatically.

### 6. Verify the service

The service should appear in **Services (`services.msc`)** as:

```text
OMAX Runtime Collector
```

with a status of:

```text
Running
```

The service can also be checked from PowerShell:

```powershell
Get-Service OMAXRuntimeCollector
```

### 7. Verify automatic startup

Restart the computer.

After Windows starts:

1. Open `services.msc`.
2. Find **OMAX Runtime Collector**.
3. Verify that its status is **Running**.
4. Check `collector.log` for a new startup entry.
5. Start `FakeOmax` if performing a local test.
6. Verify that the collector connects successfully.

The collector should start automatically without manually launching the executable.

### 8. Test service recovery

To verify that Windows can recover from an unexpected collector process failure:

First find the running collector process:

```powershell
Get-Process OMAXRuntimeCollector
```

Note the process ID (`Id`).

Then terminate the process:

```powershell
Stop-Process -Id <PROCESS_ID> -Force
```

For example:

```powershell
Stop-Process -Id 22704 -Force
```

This simulates an unexpected application failure rather than a normal service shutdown.

Windows should detect the failure and restart the service according to the configured recovery policy.

A new startup sequence should subsequently appear in `collector.log`:

```text
OMAX Runtime Collector
========================================
OMAX Runtime Collector starting.
```

### 9. Stop the service

To manually stop the service:

```powershell
sc.exe stop OMAXRuntimeCollector
```

Alternatively, the service can be stopped from `services.msc`.

### 10. Remove the service

The recommended way to remove the service and its installation directory is:

```powershell
.\uninstall-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

Alternatively, the service can be removed manually:

```powershell
sc.exe stop OMAXRuntimeCollector
sc.exe delete OMAXRuntimeCollector
```

The manual `sc.exe` commands remove the service but do not automatically delete the application files.

## Runtime Output

The collector produces a CSV file containing one row per **machine per day**:

```csv
MachineId,Date,MorningRuntime,AfternoonRuntime
OMAX-01,2026-08-31,02:35:12,04:17:45
OMAX-02,2026-08-31,01:42:30,03:08:21
```

The `MachineId` identifies which OMAX machine produced the data.

The morning and afternoon values represent the amount of time the machine was actively executing during each period.

The collector updates the existing row when runtime for the same machine and date is saved.

The default storage location is:

```text
%ProgramData%\OMAXRuntimeCollector\runtime.csv
```

The application log is stored at:

```text
%ProgramData%\OMAXRuntimeCollector\collector.log
```

The storage location is configurable through `Storage.CsvPath` and `Storage.LogPath`.

## Architecture

The application separates its responsibilities into several components:

```text
                         Windows Service
                              │
                              ▼
                    RuntimeCollectorWorker
                              │
                              ▼
                         OmaxClient
                              │
                              ▼
                       RuntimeTracker
                         │          │
                         │          ▼
                         │    RuntimeCalculator
                         │
                         ▼
                    RuntimeCsvWriter
                         │
                         ▼
                     runtime.csv
```

* **RuntimeCollectorWorker** hosts the collector as a background service and manages application lifetime.
* **OmaxClient** handles communication with the OMAX endpoint.
* **RuntimeTracker** interprets execution events and maintains the current execution state.
* **RuntimeCalculator** contains the runtime calculation logic.
* **RuntimeCsvWriter** manages the CSV output.
* **ConfigurationValidator** validates the application configuration before startup.
* **AppLogger** records application activity and errors.

Keeping the calculation logic separate from the connection and tracking components makes the core runtime behavior easier to test.

## Reliability

The collector has two levels of connection and process recovery.

### OMAX connection recovery

If the OMAX endpoint is unavailable, the collector remains running and periodically attempts to reconnect.

```text
OMAX unavailable
      │
      ▼
Connection fails
      │
      ▼
Wait configured delay
      │
      ▼
Try again
      │
      └───────► Repeat until connection succeeds
```

If the connection is lost while an execution is active, the current execution is no longer tracked until a new `ACTIVE` event is received.

This prevents the collector from inventing runtime during a period where the machine's state cannot be confirmed.

### Windows Service recovery

If the collector process itself unexpectedly terminates, Windows Service Control Manager can restart it using the configured service recovery actions.

```text
Collector process fails
          │
          ▼
Windows detects failure
          │
          ▼
Wait 5 seconds
          │
          ▼
Collector starts again
          │
          ▼
Normal connection/retry logic resumes
```

Both behaviors have been tested successfully in the development environment.

## Project Status

The project has successfully completed the initial Windows Service implementation and local deployment testing.

The following have been validated:

* Runtime calculation and time-boundary handling
* CSV creation and updates
* Multiple-machine CSV identification using `MachineId`
* Connection and reconnection behavior
* Execution state tracking
* Connection-loss handling during active execution
* Configuration validation
* Application version reporting
* Running the published executable outside the development environment
* Running as a Windows Service
* Automatic service startup after Windows reboot
* Automatic service recovery after an unexpected collector process failure
* Automated unit test suite
* PowerShell publishing, installation, and uninstallation scripts

The next stage is to validate the collector on the real OMAX machines under normal production conditions.

A future stage will also address centralized runtime storage so that multiple OMAX machines can contribute their runtime information to a shared location accessible by the appropriate users.

The final storage architecture will depend on the network location, permissions, and service-account configuration available in the production environment.

## License

This project is currently intended for internal use.
