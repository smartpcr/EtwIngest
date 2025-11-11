# ASEventReader - Standalone Solution

A standalone C# solution for reading and analyzing ETL (Event Trace Log) files from Windows ETW (Event Tracing for Windows).

## Solution Structure

```
src/standalone/
├── ASEventReader.sln              # Visual Studio solution file
├── Directory.Build.props           # Global MSBuild properties
├── Directory.Build.targets         # Global MSBuild targets
├── nuget.config                    # NuGet package sources configuration
├── README.md                       # This file
├── source/                         # Source code projects
│   └── ASEventReader/             # Main console application
│       ├── ASEventReader.csproj
│       ├── Program.cs
│       ├── README.md              # Detailed project documentation
│       ├── Models/                # Data models
│       │   ├── ASEventObject.cs
│       │   ├── EventNames.cs
│       │   └── PropertyNames.cs
│       ├── Tools/                 # Core processing logic
│       │   ├── EventProcessor.cs
│       │   ├── ASEventWrapper.cs
│       │   └── EtwScopeTracker.cs
│       └── EventFormatters/       # Event formatting extensibility
│           ├── EventFormatterBase.cs
│           └── EventFormatterMap.cs
└── tests/                         # Test projects
    ├── ASEventReaderUnitTests/   # Unit tests using MSTest
    │   ├── ASEventReaderUnitTests.csproj
    │   ├── Models/               # Tests for model classes
    │   │   ├── ASEventObjectTests.cs
    │   │   ├── EventNamesTests.cs
    │   │   └── PropertyNamesTests.cs
    │   └── Tools/                # Tests for tool classes
    │       ├── EventProcessorTests.cs
    │       └── EtwScopeTrackerTests.cs
    └── ASEventReaderIntegrationTests/  # Integration tests with real ETL files
        ├── ASEventReaderIntegrationTests.csproj
        ├── README.md             # Integration test documentation
        └── ETLFileReaderIntegrationTests.cs
```

## Building the Solution

### Prerequisites

- .NET 6.0 SDK or later
- Windows OS (required for TraceEvent library ETL file support)

### Build Commands

```bash
# Navigate to the standalone directory
cd src/standalone

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run tests
dotnet test

# Run the application
dotnet run --project source/ASEventReader -- --help
```

### Build in Visual Studio

1. Open `ASEventReader.sln` in Visual Studio 2022 or later
2. Build the solution (Ctrl+Shift+B)
3. Run tests using Test Explorer
4. Run the console application with F5 or Ctrl+F5

## Running the Application

After building, you can run the application from the output directory:

```bash
# From the standalone directory
./source/ASEventReader/bin/Debug/net6.0/ASEventReader.exe trace.etl

# Or using dotnet run
dotnet run --project source/ASEventReader -- trace.etl
```

For detailed usage instructions, see the [ASEventReader README](source/ASEventReader/README.md).

## Running Tests

### Unit Tests

```bash
# Run all tests (unit + integration)
dotnet test

# Run only unit tests
dotnet test --filter "TestCategory!=Integration"

# Run tests with verbose output
dotnet test -v normal

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Integration Tests

Integration tests read actual ETL/ZIP files from `/mnt/X/icm/IL17` and analyze their contents.

```bash
# Run only integration tests
dotnet test --filter "TestCategory=Integration"

# Or use the dedicated script (PowerShell)
.\run-integration-tests.ps1

# Or use the dedicated script (Bash)
chmod +x run-integration-tests.sh
./run-integration-tests.sh
```

**Note**: Integration tests require actual ETL/ZIP files in the test directory. If the directory doesn't exist or contains no files, tests will be marked as inconclusive.

For more details, see [Integration Tests README](tests/ASEventReaderIntegrationTests/README.md).

### Large-Scale Processing

For processing large volumes of ETL/ZIP files (40GB+, 2000+ files), use the ScalableEventProcessor with large-scale tests:

```bash
# Run large-scale tests (with parallel processing and CSV output)
dotnet test --filter "TestCategory=LargeScale"

# Or use the dedicated script (PowerShell)
.\run-largescale-tests.ps1

# Or use the dedicated script (Bash)
chmod +x run-largescale-tests.sh
./run-largescale-tests.sh
```

**Features:**
- Parallel processing scaled to CPU core count
- CSV output (no memory limitations)
- Real-time progress tracking
- Batch aggregation per file
- Final aggregates across all files

For complete documentation, see [Large-Scale Processing Guide](LARGE-SCALE-PROCESSING.md).

## Configuration Files

### Directory.Build.props

Global MSBuild properties applied to all projects in the solution:
- Sets C# language version to latest
- Enables nullable reference types
- Disables central package management (standalone solution)

### Directory.Build.targets

Global MSBuild targets. Currently empty to prevent inheritance from parent directories.

### nuget.config

Configures NuGet package sources. Uses only nuget.org for this standalone solution.

## Dependencies

The solution has minimal external dependencies:

### ASEventReader (Main Project)
- **Microsoft.Diagnostics.Tracing.TraceEvent (v3.1.8)** - Required for reading ETL files

### ASEventReaderUnitTests (Test Project)
- **Microsoft.NET.Test.Sdk (v17.8.0)** - Test SDK
- **MSTest.TestAdapter (v3.1.1)** - MSTest adapter
- **MSTest.TestFramework (v3.1.1)** - MSTest framework
- **coverlet.collector (v6.0.0)** - Code coverage collector

## Development Workflow

### Adding New Features

1. Add implementation code in `source/ASEventReader/`
2. Add corresponding unit tests in `tests/ASEventReaderUnitTests/`
3. Run tests to verify: `dotnet test`
4. Build the solution: `dotnet build`

### Adding New Event Formatters

To add custom formatting for specific ETL event providers:

1. Create a new class in `EventFormatters/` inheriting from `EventFormatterBase`
2. Implement the `ProviderGuid` property
3. Implement the `Format` method
4. The formatter will be automatically discovered and applied

See [Event Formatters Documentation](source/ASEventReader/README.md#extending-the-application) for details.

## Project Origin

This solution is extracted from the `Get-ASEvent` PowerShell cmdlet, with all PowerShell dependencies removed and converted to a standalone console application.

Original source: `/mnt/e/work/hub/common/tools/src/Diagnostics/ASDiagnostics/`

## License

Copyright (c) Microsoft Corp. All rights reserved.
