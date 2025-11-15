# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Workflow is a C# .NET 8.0 solution for processing Event Tracing for Windows (ETW) and Windows Event Log (EVTX) files, with automated ingestion into Azure Data Explorer (Kusto). The project also includes ExecutionEngine and ProgressTree libraries for workflow orchestration. All projects use BDD-style testing with Reqnroll (SpecFlow successor) and MSTest.

## Solution Structure

All projects are organized under the `src/` folder:

**ETW/EVTX Processing:**
- **EtwIngest**: Main test project containing BDD features, step definitions, and core parsing libraries
- **EtlIterator**: Batch processing console application for processing multiple ETL files in parallel
- **EtwEventReader**: Event reader for ETW events with formatters and models
- **Common.Diagnostics.EtwParser**: Core ETW/EVTX parsing library
- **Unzip**: Utility for recursively extracting nested ZIP archives containing log files

**Workflow Orchestration:**
- **ExecutionEngine**: Workflow execution engine with node-based execution model
- **ProgressTree**: Progress tracking library for workflow execution

## Build and Test Commands

```bash
# Build the solution
dotnet build Workflow.sln

# Run all tests
dotnet test src/EtwIngest/EtwIngest.csproj
dotnet test src/ExecutionEngine.UnitTests/ExecutionEngine.UnitTests.csproj
dotnet test src/ProgressTree.UnitTests/ProgressTree.UnitTests.csproj

# Run tests with specific tag
dotnet test src/EtwIngest/EtwIngest.csproj --filter "Category=parser"
dotnet test src/EtwIngest/EtwIngest.csproj --filter "Category=schema"
dotnet test src/EtwIngest/EtwIngest.csproj --filter "Category=extract"
dotnet test src/EtwIngest/EtwIngest.csproj --filter "Category=ingest"

# Build specific project
dotnet build src/EtlIterator/EtlIterator.csproj
dotnet build src/Unzip/Unzip.csproj
dotnet build src/ExecutionEngine/ExecutionEngine.csproj

# Run console applications
dotnet run --project src/EtlIterator/EtlIterator.csproj
dotnet run --project src/Unzip/Unzip.csproj
dotnet run --project src/ExecutionEngine.Example/ExecutionEngine.Example.csproj
```

## Architecture

### ETL/EVTX Processing Pipeline

1. **Parsing**: `EtlFile.Parse()` extracts event schemas from ETL files using Microsoft.Diagnostics.Tracing.TraceEvent
2. **Schema Inference**: Automatically maps .NET CLR types to Kusto column types via `KustoExtension.ToKustoColumnType()`
3. **Table Creation**: Dynamically generates Kusto DDL commands to create tables matching event schemas
4. **CSV Export**: `EtlFile.Process()` converts events to CSV format with proper escaping
5. **Ingestion**: CSV files are ingested into Kusto tables using CSV ingestion mappings

### Key Components

**EtlFile.cs**: Core ETL parser with timeout protection (stops if no events received for 10 seconds). Uses concurrent dictionaries for thread-safe event collection.

**EvtxFileParser.cs**: Windows Event Log parser using the `evtx` NuGet package to read .evtx files.

**KustoExtension.cs**: Kusto integration utilities:
- Type mapping (.NET types → Kusto types)
- Table existence checks
- DDL generation for table creation and CSV ingestion mappings

**EtlIterator/Program.cs**: Production batch processor with:
- Parallel processing of ETL files (configurable parallelism)
- Two-phase processing: schema discovery then data extraction
- Memory management with explicit GC calls between batches
- Error handling for corrupted files

### Naming Conventions

Kusto table names follow pattern: `ETL-{ProviderName}.{EventName}` where slashes in event names are removed (e.g., "StartWatchDog/Stop" becomes "StartWatchDogStop").

### BDD Testing Structure

Features are in `src/EtwIngest/Features/*.feature` with corresponding:
- Step definitions in `src/EtwIngest/Steps/*Steps.cs`
- Supporting libraries in `src/EtwIngest/Libs/`

Tests assume a local Kusto instance (Kustainer) at `http://172.24.102.61:8080` with volume mounts from `c:\kustodata`.

## Platform Considerations

- **Target Platform**: Windows-only (RuntimeIdentifier: win-x64)
- Uses Windows-specific APIs: `System.Diagnostics.EventLog`, ETW trace processing
- File paths use Windows conventions (`C:\` drive letters)
- Tests reference specific local file paths that may need adjustment

## Dependencies

Key NuGet packages:
- `Microsoft.Diagnostics.Tracing.TraceEvent` (3.1.13): ETL file parsing
- `evtx` (1.2.0): EVTX file parsing
- `Microsoft.Azure.Kusto.Data` (12.2.7): Kusto client libraries
- `Reqnroll.MsTest` (1.0.0): BDD testing framework
- `FluentAssertions` (6.12.0): Assertion library

## Development Notes

### Timeout Handling

Both `EtlFile.Parse()` and `EtlFile.Process()` include a 10-second timeout mechanism. If no events are received within 10 seconds, processing stops automatically. This prevents hanging on corrupted or malformed ETL files.

### Memory Management

EtlIterator uses aggressive memory management:
- Parallel batch processing with configurable batch sizes
- Explicit `GC.Collect()` calls after each batch
- Concurrent collections cleared and nulled after processing

### CSV Escaping

String fields containing special characters (quotes, commas, spaces, newlines) are:
1. Wrapped in double quotes
2. Internal quotes are escaped by doubling them (`"` → `""`)

### Kusto Ingestion

CSV ingestion requires:
1. Table created with matching schema
2. CSV ingestion mapping named "CsvMapping"
3. File path accessible to Kusto container (volume mount translation)
4. `ignoreFirstRecord=true` flag to skip header row
