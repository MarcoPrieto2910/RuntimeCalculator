# OMAX Runtime Collector

A C# application for monitoring the runtime of an OMAX waterjet cutting machine through its MTConnect machine data stream.

The application connects to an OMAX computer, listens for execution state changes, calculates how long the machine has been actively executing during defined work periods, and stores the results in CSV files.

The collector runs continuously as a **Windows Service**, automatically starts with Windows, reconnects when the OMAX endpoint becomes unavailable, and can recover automatically if the collector process fails.

The application supports both **local runtime storage** on each OMAX computer and a **shared CSV file** on the company's network drive so that runtime data from multiple machines can be accessed from a centralized location.

---

## Overview

The **OMAX Runtime Collector** was developed to track machine usage without requiring a third-party monitoring application.

The collector listens to the OMAX machine's MTConnect stream and identifies when a cutting operation starts and stops. From these events, it calculates the machine's active runtime and separates it into two daily accounting periods:

* **Morning:** 05:00 → 14:00
* **Afternoon:** 14:00 → 00:00

Runtime data is saved once the corresponding accounting period ends.

The collector is designed to run continuously in the background as a Windows Service. It automatically reconnects if the OMAX endpoint becomes unavailable and can be restarted automatically by Windows if the collector process unexpectedly terminates.

Each machine has its own unique `MachineId`. This allows several collectors to contribute their runtime data to a single shared CSV file.

---

## Features

* Connects directly to the OMAX machine's MTConnect TCP endpoint.
* Continuously reads machine data from the OMAX stream.
* Monitors the machine's `execution` state.
* Detects `ACTIVE` and supported ending execution states.
* Calculates runtime without relying on a dedicated runtime value from the machine.
* Separates runtime into morning and afternoon accounting periods.
* Handles executions crossing:

    * 05:00
    * 14:00
    * Midnight
* Handles multiple executions during the same accounting period.
* Automatically saves runtime data to CSV.
* Stores a unique machine identifier with each runtime record.
* Maintains a local CSV file on each OMAX computer.
* Maintains a shared CSV file on the company network drive.
* Coordinates access to the shared CSV using a lock file.
* Treats local storage as critical and shared storage as non-critical.
* Continues operating if the shared network storage is temporarily unavailable.
* Logs application activity, connection problems, and storage errors.
* Automatically reconnects when the OMAX endpoint becomes unavailable.
* Handles connection loss during active execution without inventing runtime.
* Validates configuration during application startup.
* Supports configuration through `appsettings.json`.
* Runs as a Windows Service using the .NET Generic Host and `BackgroundService`.
* Can automatically start when Windows starts.
* Can automatically restart after an unexpected process failure.
* Includes application version information in startup diagnostics.
* Includes automated unit tests for runtime calculation, execution tracking, CSV writing, configuration validation, time boundaries, and writer failure handling.
* Includes a fake OMAX server for local development and testing.
* Includes PowerShell scripts for publishing, installing, and uninstalling the Windows Service.

---

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
│   │   ├── RuntimeTracker.cs
│   │   └── Writer/
│   │       ├── IRuntimeWriter.cs
│   │       ├── RuntimeCsvWriterBase.cs
│   │       ├── RuntimeLocalCsvWriter.cs
│   │       └── RuntimeSharedCsvWriter.cs
│   │
│   ├── scripts/
│   │   ├── install-service.ps1
│   │   ├── publish.ps1
│   │   └── uninstall-service.ps1
│   │
│   ├── AppLogger.cs
│   ├── Program.cs
│   ├── RuntimeCollectorWorker.cs
│   ├── appsettings.json
│   └── OMAXRuntimeCollector.csproj
│
├── OMAXRuntimeCollector.Tests/
│   ├── Runtime/
│   │   ├── RuntimeCalculatorTests.cs
│   │   ├── Tracker/
│   │   │   ├── RuntimeTrackerBoundaryTests.cs
│   │   │   ├── RuntimeTrackerExecutionStateTests.cs
│   │   │   └── RuntimeTrackerWriterTests.cs
│   │   │
│   │   └── Writers/
│   │       ├── RuntimeLocalCsvWriterTests.cs
│   │       └── RuntimeSharedCsvWriterTests.cs
│   │
│   ├── ConfigurationValidatorTests.cs
│   └── OMAXRuntimeCollector.Tests.csproj
│
├── FakeOmax/
│   └── FakeOmax/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── stream-test.txt
│       ├── stream-test2.txt
│       └── FakeOmax.csproj
│
├── README.md
└── .gitignore
```

### Projects

#### OMAXRuntimeCollector

The main application.

It connects to the OMAX endpoint, processes machine events, calculates runtime, and writes the results to local and shared CSV storage.

The application is hosted using the .NET Generic Host and runs its collection logic through `RuntimeCollectorWorker`, which derives from `BackgroundService`.

During startup, the application:

1. Loads `appsettings.json`.
2. Deserializes the configuration.
3. Validates the configuration.
4. Creates the application logger.
5. Creates the runtime writers.
6. Creates the runtime tracker.
7. Starts the time-boundary monitor.
8. Connects to the OMAX endpoint.

When installed as a Windows Service, the collector runs in the background without requiring a user to manually launch the application.

#### OMAXRuntimeCollector.Tests

Contains the automated unit tests for the application.

The tests are organized by the component or subsystem they cover.

The test suite covers:

* Runtime calculation
* Execution state tracking
* Time-boundary processing
* Runtime writer behavior
* Shared CSV locking
* Writer failure handling
* Configuration validation

#### FakeOmax

A lightweight local server used to simulate the OMAX endpoint during development.

Instead of connecting to a real machine, it reads test data from text files and exposes the simulated stream through a TCP endpoint.

This makes it possible to test the collector without requiring access to a physical OMAX machine.

---

# Requirements

* Windows
* .NET 9 SDK
* Access to an OMAX machine running the required MTConnect TCP endpoint for production use

For development and automated testing, an OMAX machine is not required because the `FakeOmax` project can simulate the machine connection.

For deployment to the current OMAX environment, the application is published as a **self-contained 32-bit (`win-x86`) executable**.

---

# Configuration

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
    "LocalCsvPath": "%ProgramData%\\OMAXRuntimeCollector\\runtime.csv",
    "SharedCsvPath": "P:\\OMAXRuntimeCollector\\runtime.csv",
    "LogPath": "%ProgramData%\\OMAXRuntimeCollector\\collector.log"
  },
  "TestMode": false
}
```

## Machine ID

`MachineId` identifies the OMAX machine associated with the collector.

Each OMAX machine should have its own unique identifier.

For example:

```json
"MachineId": "OMAX-01"
```

The machine ID is included in the CSV output so that runtime data from multiple machines can be distinguished when stored together.

For example:

```csv
MachineId,Date,MorningRuntime,AfternoonRuntime
OMAX-01,2026-09-17,03:31:11,00:30:48
OMAX-02,2026-09-17,02:14:32,01:21:05
```

## OMAX settings

| Setting                 | Description                                                     |
|-------------------------|-----------------------------------------------------------------|
| `Host`                  | Hostname or IP address of the OMAX computer                     |
| `Port`                  | Port used by the OMAX MTConnect endpoint                        |
| `ReconnectDelaySeconds` | Delay before attempting to reconnect after a connection failure |

## Storage settings

| Setting         | Description                                               |
|-----------------|-----------------------------------------------------------|
| `LocalCsvPath`  | Local CSV file used for machine-level runtime persistence |
| `SharedCsvPath` | Shared CSV file used for centralized runtime data         |
| `LogPath`       | Location of the application log                           |

Environment variables such as `%ProgramData%` are expanded automatically.

### Local CSV

The default local CSV location is:

```text
%ProgramData%\OMAXRuntimeCollector\runtime.csv
```

This file is the **authoritative local runtime storage** for the collector.

Each OMAX computer maintains its own local copy.

### Shared CSV

The default shared CSV location is:

```text
P:\OMAXRuntimeCollector\runtime.csv
```

The shared CSV is intended to contain runtime data from all OMAX machines.

The current production environment provides the `P:\` drive to the OMAX computers and office computers with read/write access.

Each collector uses its `MachineId` to identify its own rows in the shared CSV.

The shared storage is considered a secondary copy. If the network drive becomes temporarily unavailable, the collector continues operating and the local CSV remains available.

The actual network-share integration still needs to be validated on the production network.

## TestMode

`TestMode` controls whether application log messages are also written to the console.

```json
"TestMode": true
```

is useful during development and local testing.

For Windows Service deployment, it should normally be:

```json
"TestMode": false
```

The application log is still written to the configured log file.

---

# Configuration Validation

The application validates its configuration during startup before attempting to connect to the OMAX endpoint.

The following settings are validated:

* `MachineId` must not be empty.
* `Omax.Host` must not be empty.
* `Omax.Port` must be between 1 and 65535.
* `Omax.ReconnectDelaySeconds` must be greater than 0.
* `Storage.LocalCsvPath` must not be empty.
* `Storage.SharedCsvPath` must not be empty.
* `Storage.LogPath` must not be empty.

The application validates that the shared path is configured, but does **not** require the network share to be reachable during startup.

This allows the collector to continue operating with local storage if the shared network drive is temporarily unavailable.

If configuration errors are detected, the application reports the errors and stops before attempting to connect to the OMAX endpoint.

---

# Running the Application

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

---

# Running with the Fake OMAX Server

The `FakeOmax` project can be used to simulate the OMAX machine during development.

Start the fake server first:

```powershell
dotnet run --project FakeOmax/FakeOmax
```

Then start the collector:

```powershell
dotnet run --project OMAXRuntimeCollector
```

The fake server reads one of the provided test streams and exposes the simulated events through its configured TCP endpoint.

This allows the collector to be tested without connecting to a physical machine.

---

# Testing

Run all automated tests from the repository root:

```powershell
dotnet test
```

The test suite covers:

### Runtime calculation

* Runtime within a single accounting period
* Runtime crossing accounting boundaries
* Multiple executions
* Morning and afternoon calculations

### Runtime tracking

* `ACTIVE` execution events
* Supported ending execution states
* Ignored non-executing states
* Duplicate `ACTIVE` events
* Connection loss during active execution
* 14:00 boundary processing
* Midnight boundary processing

### CSV writers

* CSV creation
* Morning runtime storage
* Afternoon runtime storage
* Updating existing rows
* Multiple dates
* Multiple machines
* Shared CSV locking
* Waiting for an existing lock
* Lock timeout behavior
* Concurrent writer behavior

### Writer failure handling

Runtime writers declare whether they are **critical** through the `IRuntimeWriter.IsCritical` property.

The tests verify that:

* A failed non-critical writer does not stop the collector.
* A failed critical writer propagates its exception.
* A failed non-critical writer does not prevent other writers from running.
* A failed critical writer prevents subsequent writers from being called.

The local CSV writer is currently critical because it is the authoritative local persistence mechanism.

The shared CSV writer is non-critical because it is a secondary centralized copy.

### Configuration

* Required machine ID
* OMAX host
* Valid port range
* Reconnect delay
* Local CSV path
* Shared CSV path
* Log path

All automated tests should pass before publishing a new version.

---

# Building the Executable

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

---

# PowerShell Scripts

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

## Publish

From the `scripts` directory:

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

## Install the Windows Service

After copying the published files to the target installation directory, open **PowerShell as Administrator**.

Run:

```powershell
.\install-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

The installation script:

1. Verifies administrator privileges.
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

## Uninstall the Windows Service

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

## Script Requirements

The installation and uninstallation scripts require an **elevated PowerShell session** because creating, removing, and configuring Windows Services requires administrator privileges.

The publish script does not require administrator privileges when publishing from a normal development environment.

---

# Windows Service Deployment

The collector can be installed as a Windows Service so that it runs automatically in the background and starts with Windows.

The recommended deployment process is:

## 1. Run the tests

From the repository root:

```powershell
dotnet test
```

Make sure all tests pass before publishing.

## 2. Publish the application

From the `OMAXRuntimeCollector/scripts` directory:

```powershell
.\publish.ps1
```

## 3. Copy the published files

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

## 4. Configure the application

Before starting the service, verify `appsettings.json`.

For a production installation, `TestMode` should normally be:

```json
"TestMode": false
```

Also verify:

* `MachineId`
* OMAX host
* OMAX port
* reconnect delay
* local CSV path
* shared CSV path
* log path

Each OMAX machine should have a unique `MachineId`.

## 5. Install the service

Open **PowerShell as Administrator** and run:

```powershell
.\install-service.ps1 -InstallPath "C:\OMAXRuntimeCollector"
```

The script creates and starts the service automatically.

## 6. Verify the service

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

## 7. Verify automatic startup

Restart the computer.

After Windows starts:

1. Open `services.msc`.
2. Find **OMAX Runtime Collector**.
3. Verify that its status is **Running**.
4. Check `collector.log` for a new startup entry.
5. Verify that the collector attempts to connect to the configured OMAX endpoint.

The collector should start automatically without manually launching the executable.

## 8. Test service recovery

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

## 9. Stop the service

To manually stop the service:

```powershell
sc.exe stop OMAXRuntimeCollector
```

Alternatively, the service can be stopped from `services.msc`.

## 10. Remove the service

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

---

# Runtime Output

The collector produces CSV data containing one row per **machine per day**:

```csv
MachineId,Date,MorningRuntime,AfternoonRuntime
OMAX-01,2026-09-17,03:31:11,00:30:48
OMAX-02,2026-09-17,02:14:32,01:21:05
```

The `MachineId` identifies which OMAX machine produced the data.

The morning and afternoon values represent the amount of time the machine was actively executing during each accounting period.

The collector updates the existing row when runtime for the same machine and date is saved.

## Local Runtime CSV

Each OMAX computer maintains its own local runtime CSV.

The default location is:

```text
%ProgramData%\OMAXRuntimeCollector\runtime.csv
```

This local CSV is considered the authoritative persistence for that collector.

## Shared Runtime CSV

The collectors can also write to a centralized shared CSV:

```text
P:\OMAXRuntimeCollector\runtime.csv
```

The shared file contains rows from multiple machines.

For example:

```csv
MachineId,Date,MorningRuntime,AfternoonRuntime
OMAX-01,2026-09-17,03:31:11,00:30:48
OMAX-02,2026-09-17,02:14:32,01:21:05
OMAX-03,2026-09-17,04:02:19,01:07:44
```

The shared writer uses a companion lock file:

```text
P:\OMAXRuntimeCollector\runtime.csv.lock
```

The lock prevents multiple collectors from simultaneously reading and modifying the shared CSV.

The shared writer waits for an existing lock for a configurable amount of time. If the lock cannot be acquired within the configured timeout, the write fails and the error is reported to the runtime tracker.

The shared writer is **non-critical**. A failure to access the network share does not stop the collector or prevent local runtime persistence.

The actual multi-computer SMB/network-share integration still needs to be validated on the production network.

## Application Log

The application log is stored by default at:

```text
%ProgramData%\OMAXRuntimeCollector\collector.log
```

Startup diagnostics include information such as:

* Application version
* Machine ID
* OMAX host
* OMAX port
* Local CSV path
* Shared CSV path
* Log path

---

# Architecture

The application separates connection handling, runtime calculation, state tracking, and persistence into dedicated components.

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
                    IRuntimeWriter
                         │
                 ┌───────┴────────┐
                 │                │
                 ▼                ▼
        RuntimeLocalCsvWriter  RuntimeSharedCsvWriter
           Critical = true      Critical = false
                 │                │
                 ▼                ▼
          Local runtime.csv   Shared runtime.csv
```

## RuntimeCollectorWorker

Hosts the collector as a .NET `BackgroundService`.

It manages:

* Configuration loading
* Configuration validation
* Logger creation
* Runtime writer creation
* Runtime tracker creation
* Time-boundary monitoring
* OMAX client lifetime
* Application shutdown

## OmaxClient

Handles communication with the OMAX endpoint.

It:

* Connects to the configured host and port.
* Reads the machine data stream.
* Passes received lines to `RuntimeTracker`.
* Attempts to reconnect when the connection is lost.

## RuntimeTracker

Interprets machine execution states and maintains the current runtime state.

It:

* Detects the beginning of active execution.
* Detects supported execution-ending states.
* Prevents duplicate `ACTIVE` events from restarting an execution.
* Handles 14:00 and midnight boundaries.
* Handles connection loss.
* Passes execution intervals to `RuntimeCalculator`.
* Sends completed runtime periods to the configured writers.

RuntimeTracker operates against the `IRuntimeWriter` abstraction rather than knowing which storage systems are being used.

## RuntimeCalculator

Contains the runtime calculation logic.

It determines how an execution interval should be divided between:

```text
05:00 → 14:00  Morning
14:00 → 00:00  Afternoon
```

This keeps the calculation logic independent from network communication and file storage.

## IRuntimeWriter

Defines the persistence interface used by `RuntimeTracker`.

A runtime writer provides:

* `SaveMorningRuntime()`
* `SaveAfternoonRuntime()`
* `IsCritical`

The `IsCritical` property determines how a storage failure is handled.

### Critical writer

A critical writer failure is propagated to the caller.

The current local CSV writer is critical because local storage is the authoritative persistence mechanism.

### Non-critical writer

A non-critical writer failure is logged but does not stop the collector.

The current shared CSV writer is non-critical because the shared CSV is a secondary centralized copy.

This abstraction allows additional persistence mechanisms to be added in the future without requiring `RuntimeTracker` to know their concrete types.

## RuntimeCsvWriterBase

Contains the common CSV reading, updating, and writing logic shared by the local and shared CSV writers.

It:

* Creates required directories.
* Creates the CSV header when needed.
* Locates rows by `MachineId` and date.
* Updates existing rows.
* Creates new rows when necessary.
* Preserves the other runtime period when updating a row.

## RuntimeLocalCsvWriter

Writes runtime data to the local CSV file.

It is configured as:

```text
IsCritical = true
```

## RuntimeSharedCsvWriter

Writes runtime data to the shared network CSV.

It is configured as:

```text
IsCritical = false
```

It uses a `.lock` file with exclusive file access to coordinate concurrent writes from multiple collectors.

---

# Reliability

The collector has several levels of recovery and failure handling.

## OMAX connection recovery

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

If the connection is lost while an execution is active, the current execution is discarded.

The collector waits for a new `ACTIVE` event before starting a new execution interval.

This prevents the collector from inventing runtime during a period where the machine's state cannot be confirmed.

## Local storage failure

The local CSV is considered critical.

If the local writer fails, the exception is propagated rather than silently ignored.

This prevents the collector from treating runtime as successfully persisted when the authoritative local storage operation failed.

## Shared storage failure

The shared CSV is considered non-critical.

If the network drive is unavailable, the shared writer failure is logged and the collector continues operating.

The local CSV remains available as the authoritative local record.

```text
Shared storage unavailable
          │
          ▼
Shared write fails
          │
          ▼
Error logged
          │
          ▼
Collector continues
          │
          ▼
Local storage remains available
```

## Windows Service recovery

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

The configured recovery policy allows three restart attempts with a five-second delay between attempts. The failure count resets after 24 hours.

These Windows Service recovery behaviors have been tested successfully in the development environment.

---

# Current Project Status

The project has completed the main Windows Service implementation and local development/deployment testing.

The following have been validated through automated tests or development-environment testing:

* Runtime calculation
* Multiple executions
* Execution state tracking
* 05:00 boundary handling
* 14:00 boundary handling
* Midnight boundary handling
* CSV creation and updates
* Multiple-machine CSV identification using `MachineId`
* Shared writer lock behavior
* Shared writer timeout behavior
* Concurrent writer behavior
* Critical and non-critical writer failure handling
* Connection and reconnection behavior
* Connection-loss handling during active execution
* Configuration validation
* Application version reporting
* Running the published executable outside the development environment
* Running as a Windows Service
* Automatic service startup after Windows reboot
* Automatic service recovery after an unexpected collector process failure
* Automated unit test suite
* PowerShell publishing, installation, and uninstallation scripts

## Remaining production validation

The next major validation stage is testing the collector on the real OMAX machines and production network.

This includes:

* Installing the collector on the actual OMAX computers.
* Confirming the correct `MachineId` on each machine.
* Confirming communication with each OMAX endpoint.
* Confirming local CSV persistence.
* Confirming access to `P:\OMAXRuntimeCollector\`.
* Confirming that multiple machines can safely update the same shared CSV.
* Confirming that the `.lock` file coordinates writes correctly across computers.
* Confirming behavior when the shared network drive is temporarily unavailable.
* Confirming normal runtime collection during real machine operation.

The shared network integration has been tested through automated unit tests, including locking and concurrent writer scenarios, but the actual multi-computer network-share behavior has not yet been validated in the production environment.

---

# License

This project is currently intended for internal use.
