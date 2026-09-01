# OMAX Runtime Collector

A C# application for monitoring the runtime of an OMAX waterjet cutting machine through its machine log stream.

The application connects to an OMAX computer, listens for execution state changes, calculates how long the machine has been running during defined work periods, and stores the results in a CSV file.

## Overview

The **OMAX Runtime Collector** was developed to track machine usage without requiring a third-party monitoring application.

The collector listens to the OMAX log stream and identifies when a cutting job starts and stops. From these events, it calculates the machine's active runtime and separates it into two daily periods:

* **Morning:** 05:00 → 14:00
* **Afternoon:** 14:00 → 00:00

Runtime data is periodically written to a CSV file for later analysis.

The application is designed to run continuously in the background and automatically reconnect if the OMAX connection is temporarily unavailable.

## Features

* Connects to an OMAX machine through its HTTP endpoint.
* Monitors machine execution state.
* Detects `ACTIVE` and `STOPPED` execution events.
* Calculates runtime without relying on a dedicated runtime value from the machine.
* Separates runtime into morning and afternoon periods.
* Handles executions crossing:

  * 05:00
  * 14:00
  * Midnight
* Automatically saves runtime data to CSV.
* Logs application activity and connection problems.
* Automatically attempts to reconnect when the OMAX endpoint becomes unavailable.
* Configurable connection and storage settings through `appsettings.json`.
* Includes automated unit tests for runtime calculation, CSV writing, execution state tracking, and time boundaries.
* Includes a fake OMAX server for local development and testing.

## Repository Structure

```text
RuntimeCollector/
│
├── OMAXRuntimeCollector/
│   ├── OmaxConnection/
│   │   ├── OmaxClient.cs
│   │   └── OmaxSettings.cs
│   │
│   ├── Runtime/
│   │   ├── RuntimeCalculator.cs
│   │   ├── RuntimeCsvWriter.cs
│   │   └── RuntimeTracker.cs
│   │
│   ├── AppLogger.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── OMAXRuntimeCollector.csproj
│
├── OMAXRuntimeCollector.Tests/
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

#### OMAXRuntimeCollector.Tests

Contains the automated unit tests for the application.

The tests cover the runtime calculation logic, CSV output, execution state transitions, and time-boundary behavior.

#### FakeOmax

A lightweight local server used to simulate the OMAX endpoint during development.

Instead of connecting to a real machine, it reads test data from text files and exposes it through the same HTTP endpoint expected by the collector.

This makes it possible to test the collector without requiring access to the physical machine.

## Requirements

* Windows
* .NET 8 SDK
* Access to an OMAX machine running the required HTTP endpoint

For development and testing, an OMAX machine is not required because the `FakeOmax` project can simulate the machine connection.

## Configuration

The collector uses `appsettings.json` for its configuration.

Example:

```json
{
  "Omax": {
    "Host": "localhost",
    "Port": 5000,
    "Endpoint": "/probe",
    "ReconnectDelaySeconds": 5
  },
  "Storage": {
    "CsvPath": "%ProgramData%\\OMAXRuntimeCollector\\runtime.csv",
    "LogPath": "%ProgramData%\\OMAXRuntimeCollector\\collector.log"
  }
}
```

### OMAX settings

| Setting                 | Description                                    |
| ----------------------- | ---------------------------------------------- |
| `Host`                  | Hostname or IP address of the OMAX computer    |
| `Port`                  | Port used by the OMAX endpoint                 |
| `Endpoint`              | HTTP endpoint providing the machine log stream |
| `ReconnectDelaySeconds` | Delay before attempting to reconnect           |

### Storage settings

| Setting   | Description                           |
| --------- | ------------------------------------- |
| `CsvPath` | Location where runtime data is stored |
| `LogPath` | Location of the application log       |

Environment variables such as `%ProgramData%` are expanded automatically.

## Running the Application

From the `OMAXRuntimeCollector` project directory:

```powershell
dotnet run
```

The application will connect to the configured OMAX endpoint and begin monitoring machine activity.

To stop the application, press:

```text
Ctrl+C
```

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

The fake server reads one of the provided test streams and exposes the simulated events through the configured HTTP endpoint.

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
* Execution state transitions
* Runtime processing at time boundaries

## Runtime Output

The collector produces a CSV file containing one row per day:

```csv
Date,MorningRuntime,AfternoonRuntime
2026-08-31,02:35:12,04:17:45
2026-09-01,01:42:30,03:08:21
```

The morning and afternoon values represent the amount of time the machine was actively executing during each period.

## Architecture

The application separates its responsibilities into several components:

```text
                 OMAX Machine
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

* **OmaxClient** handles communication with the OMAX endpoint.
* **RuntimeTracker** interprets execution events and maintains the current execution state.
* **RuntimeCalculator** contains the runtime calculation logic.
* **RuntimeCsvWriter** manages the CSV output.
* **AppLogger** records application activity and errors.

Keeping the calculation logic separate from the connection and tracking components also makes the core runtime behavior easier to test.

## Project Status

The project is currently intended for deployment and testing on an OMAX machine in a production environment.

The next stage is real-machine validation to verify that the collector behaves correctly with the actual OMAX log stream and normal machine operation.

## License

This project is currently intended for internal use.
