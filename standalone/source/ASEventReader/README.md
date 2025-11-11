# ASEventReader

A standalone C# console application for reading and analyzing ETL (Event Trace Log) files from Windows ETW (Event Tracing for Windows).

## Overview

This project is extracted from the `Get-ASEvent` PowerShell cmdlet and converted into a standalone console application. It reads ETL trace files, processes events, and displays them in various formats.

## Features

- Read ETL files from individual files, directories, or wildcards
- Support for ZIP files containing ETL files
- Filter events by:
  - Activity ID
  - Provider Name
  - Event Name
- Multiple output formats:
  - **Summary**: Compact view showing timestamp, status, event type, and duration
  - **Detailed**: Full event details including all properties
  - **Tree**: Hierarchical view of events
- Error reporting and analysis
- Automatic pairing of Start/Stop events with duration calculation

## Dependencies

The project has minimal external dependencies:

- **.NET 6.0 or later**: Target framework
- **Microsoft.Diagnostics.Tracing.TraceEvent (v3.1.8)**: Required for reading ETL files (this is the only external dependency)

## Building the Project

```bash
cd ASEventReader
dotnet build
```

## Running the Application

### Basic Usage

```bash
dotnet run -- trace.etl
```

Or after building:

```bash
./bin/Debug/net6.0/ASEventReader trace.etl
```

### Examples

1. **Read a single ETL file with summary output:**
   ```bash
   ASEventReader trace.etl
   ```

2. **Read all ETL files in a directory with detailed output:**
   ```bash
   ASEventReader -f detailed C:\Logs\
   ```

3. **Filter by provider name:**
   ```bash
   ASEventReader -p "Microsoft-Windows-Kernel-Process" trace.etl
   ```

4. **Filter by event name with tree output:**
   ```bash
   ASEventReader -e "ProcessStart" -f tree *.etl
   ```

5. **Filter by activity ID:**
   ```bash
   ASEventReader -a "12345678-1234-1234-1234-123456789012" trace.etl
   ```

6. **Show detailed error information:**
   ```bash
   ASEventReader --errors trace.etl
   ```

## Command Line Options

```
Usage: ASEventReader [options] <path1> [path2] ...

Options:
  -h, --help              Show help message
  -a, --activity <guid>   Filter by Activity ID
  -p, --provider <name>   Filter by Provider Name
  -e, --event <name>      Filter by Event Name
  -f, --format <format>   Output format: detailed, summary, tree (default: summary)
  --errors                Show detailed error information

Arguments:
  <path>                  Path to ETL file(s) or directory containing ETL files
                          Supports wildcards (e.g., *.etl)
                          Supports zip files containing ETL files
```

## Output Formats

### Summary Format (Default)
```
[2025-01-15 10:30:45.123] SUCCESS - ProcessStart (15ms)
[2025-01-15 10:30:45.138] SUCCESS - ProcessStop
[2025-01-15 10:30:45.140] FAIL - ProcessError
```

### Detailed Format
Shows all event properties including:
- Event Type
- Timestamp
- Success status
- Duration (if available)
- All custom properties from the event payload
- Hierarchy level

### Tree Format
Displays events in a hierarchical tree structure showing parent-child relationships between events.

## Project Structure

```
ASEventReader/
├── ASEventReader.csproj          # Project file
├── Program.cs                     # Main entry point
├── Models/                        # Data models
│   ├── ASEventObject.cs          # Event wrapper class
│   ├── EventNames.cs             # Well-known event names
│   └── PropertyNames.cs          # Well-known property names
├── Tools/                         # Core logic
│   ├── EventProcessor.cs         # Main processing logic
│   ├── ASEventWrapper.cs         # Wraps TraceEvent into ASEventObject
│   └── EtwScopeTracker.cs       # Tracks event scopes (Start/Stop pairs)
└── EventFormatters/               # Event formatting
    ├── EventFormatterBase.cs     # Base class for custom formatters
    └── EventFormatterMap.cs      # Maps formatters to providers
```

## Architecture

### Core Components

1. **EventProcessor**: Main processing engine
   - Resolves file paths (supports wildcards, directories, zip files)
   - Reads ETL files using TraceEvent library
   - Applies filters (Activity ID, Provider Name, Event Name)
   - Manages temporary extraction directories for zip files

2. **ASEventWrapper**: Event conversion
   - Converts TraceEvent objects to ASEventObject instances
   - Pairs Start/Stop events automatically
   - Calculates event duration
   - Builds event hierarchy

3. **ASEventObject**: Event data model
   - Stores event properties
   - Maintains parent-child relationships
   - Supports tree visualization
   - Provides formatted output

4. **EtwScopeTracker**: Scope management
   - Tracks open event scopes (Start events waiting for Stop)
   - Matches Start/Stop event pairs
   - Uses unique event identifiers (Provider + Task + Process + Activity)

### Event Processing Flow

1. Parse command line arguments
2. Resolve input paths (expand wildcards, extract zip files)
3. For each ETL file:
   - Create ETWTraceEventSource
   - Register event callbacks with filters
   - Process events
   - Wrap events in ASEventObject
   - Track Start/Stop pairs
4. Display results in requested format
5. Clean up temporary files

## Extending the Application

### Adding Custom Event Formatters

To add custom formatting for specific event providers:

1. Create a new class inheriting from `EventFormatterBase`
2. Implement the `ProviderGuid` property
3. Implement the `Format` method to customize the event
4. The formatter will be automatically discovered and applied

Example:
```csharp
internal class CustomEventFormatter : EventFormatterBase
{
    public override Guid ProviderGuid => new Guid("12345678-1234-1234-1234-123456789012");

    protected override void Format(TraceEvent traceEvent, ASEventObject asEventObject)
    {
        // Custom formatting logic
        asEventObject.AddProperty("CustomProperty", "CustomValue");
    }
}
```

## Original Source

This code is extracted from the `Get-ASEvent` PowerShell cmdlet located in:
- `/mnt/e/work/hub/common/tools/src/Diagnostics/ASDiagnostics/`

The PowerShell-specific code has been removed and replaced with console application logic while preserving all the core ETL processing functionality.

## License

Copyright (c) Microsoft Corp. All rights reserved.
