# Common.Diagnostics.EtwParser

A comprehensive .NET 8.0 library for parsing Event Tracing for Windows (ETW) `.etl` files and Windows Event Log (EVTX) `.evtx` files with schema inference and data extraction capabilities.

## Features

- **ETL File Parsing**: Parse ETW trace files with full schema discovery
- **EVTX File Parsing**: Parse Windows Event Log files
- **Schema Inference**: Automatically discover event schemas from trace files
- **Type Mapping**: Map .NET types to Kusto, SQL Server, and other type systems
- **CSV Export**: Export events to CSV format with proper escaping
- **Batch Processing**: Process multiple trace files in parallel with configurable concurrency
- **Memory Efficient**: Aggressive garbage collection and batch processing support
- **Timeout Protection**: Automatic timeout for corrupted or incomplete trace files
- **Extensible**: Interface-based design for custom parsers and exporters

## Installation

Add the project reference to your solution:

```xml
<ItemGroup>
  <ProjectReference Include="..\Common.Diagnostics.EtwParser\Common.Diagnostics.EtwParser.csproj" />
</ItemGroup>
```

## Quick Start

### Parse a Single ETL File

```csharp
using Common.Diagnostics.EtwParser.Parsers;
using Common.Diagnostics.EtwParser.Models;

// Create parser
var parser = new EtlFileParser("trace.etl");

// Discover schemas
var schemas = new Dictionary<EventIdentifier, TraceEventSchema>();
var result = parser.DiscoverSchemas(schemas);

if (result.Success)
{
    Console.WriteLine($"Discovered {schemas.Count} event types");

    // Extract events
    var events = parser.ExtractEvents(schemas);

    foreach (var kvp in events)
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value.Count} events");
    }
}
```

### Batch Process Multiple Files

```csharp
using Common.Diagnostics.EtwParser.Core;

var processor = new BatchTraceProcessor(
    maxDegreeOfParallelism: Environment.ProcessorCount,
    batchSize: 10
);

var files = Directory.GetFiles(@"C:\traces", "*.etl");

// Discover all schemas
var allSchemas = processor.DiscoverSchemas(files, (processed, total) =>
{
    Console.WriteLine($"Schema discovery: {processed}/{total}");
});

// Extract all events
var allEvents = processor.ExtractEvents(files, allSchemas, (processed, total) =>
{
    Console.WriteLine($"Event extraction: {processed}/{total}");
});
```

### Export to CSV

```csharp
using Common.Diagnostics.EtwParser.Extensions;

var exporter = new CsvExporter(
    outputDirectory: @"C:\output",
    filePrefix: "ETL",
    includeHeaders: true
);

var exportResult = await exporter.ExportAsync(events, schemas);

if (exportResult.Success)
{
    Console.WriteLine($"Exported {exportResult.RecordCount} records to {exportResult.OutputLocation}");
}
```

### Generate Kusto DDL

```csharp
using Common.Diagnostics.EtwParser.Extensions;

foreach (var schema in schemas.Values)
{
    // Generate CREATE TABLE command
    var createTable = schema.ToKustoCreateTableCommand();
    Console.WriteLine(createTable);

    // Generate CSV ingestion mapping
    var csvMapping = schema.ToKustoCsvMappingCommand();
    Console.WriteLine(csvMapping);
}
```

## Architecture

### Core Interfaces

- **`ITraceEventParser`**: Interface for trace file parsers
- **`IEventExporter`**: Interface for event exporters

### Models

- **`EventIdentifier`**: Uniquely identifies an event type (ProviderName, EventName)
- **`TraceEventSchema`**: Schema definition with ordered field list
- **`FieldSchema`**: Field definition with name, type, and metadata
- **`TraceEventRecord`**: Single event record with field values
- **`ParseResult`**: Result of parsing operation with success/error info

### Parsers

- **`EtlFileParser`**: Parses ETW `.etl` files using Microsoft.Diagnostics.Tracing.TraceEvent
- **`EvtxFileParser`**: Parses Windows Event Log `.evtx` files using evtx library

### Utilities

- **`TypeMapper`**: Maps .NET types to Kusto, SQL Server, etc.
- **`BatchTraceProcessor`**: Parallel batch processing with memory management
- **`CsvExporter`**: Exports events to CSV with proper escaping
- **`KustoExtensions`**: Generate Kusto DDL and ingestion mappings

## Advanced Usage

### Custom Event Exporter

```csharp
using Common.Diagnostics.EtwParser.Core;
using Common.Diagnostics.EtwParser.Models;

public class JsonExporter : IEventExporter
{
    public async Task<ExportResult> ExportAsync(
        IDictionary<EventIdentifier, IList<TraceEventRecord>> events,
        IDictionary<EventIdentifier, TraceEventSchema> schemas)
    {
        // Your export logic here
        return ExportResult.Successful(eventCount, outputPath);
    }
}
```

### Schema Filtering

```csharp
// Filter schemas by provider
var filteredSchemas = allSchemas
    .Where(kvp => kvp.Key.ProviderName.StartsWith("Microsoft-AzureStack"))
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

// Extract only filtered events
var filteredEvents = parser.ExtractEvents(filteredSchemas);
```

### Batch Processing with Custom Logic

```csharp
var processor = new BatchTraceProcessor(batchSize: 5);

await processor.ProcessInBatchesAsync(
    files,
    allSchemas,
    exporter,
    batchProgressCallback: (batchNum, totalBatches) =>
    {
        Console.WriteLine($"Completed batch {batchNum}/{totalBatches}");

        // Custom cleanup or checkpoint logic here
    }
);
```

## Event Schema Structure

### Standard Fields (All Events)

- **TimeStamp** (DateTime): Event timestamp
- **ProcessID** (int): Process ID that generated the event
- **ProcessName** (string): Process name
- **Level** (int): Event level (severity)
- **Opcode** (int): Operation code
- **OpcodeName** (string): Operation name

### Payload Fields

Event-specific custom fields extracted from the trace payload. Types are inferred automatically:

- Primitive types: int, long, bool, string, etc.
- Complex types: Mapped to "dynamic" in Kusto

## Type Mapping

### Kusto Types

| .NET Type | Kusto Type |
|-----------|------------|
| string | string |
| DateTime | datetime |
| int, byte, short | int |
| long | long |
| decimal, float, double | real |
| bool | bool |
| Guid | guid |
| TimeSpan | timespan |
| Enum | string |
| Complex | dynamic |

### SQL Server Types

| .NET Type | SQL Type |
|-----------|----------|
| string | NVARCHAR(MAX) |
| DateTime | DATETIME2 |
| int | INT |
| long | BIGINT |
| decimal | DECIMAL(18,2) |
| bool | BIT |

## Performance Considerations

### Memory Management

- Use `BatchTraceProcessor` with appropriate batch sizes for large file sets
- Explicit GC collection between batches to free memory
- Concurrent collections for thread-safe schema accumulation

### Parallelism

- Default parallelism: `Environment.ProcessorCount`
- Configurable via `BatchTraceProcessor` constructor
- Lock-free operations where possible

### Timeout Protection

- Default 10-second inactivity timeout for ETL parsing
- Prevents hanging on corrupted files
- Configurable via `EtlFileParser` constructor

## Naming Conventions

### Kusto Table Names

Tables are named using the pattern: `ETL-{ProviderName}.{EventName}`

Examples:
- `ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2`
- `ETL-Microsoft-AzureStack-Compute.ProcessStarted`

Slashes in event names are removed:
- `StartWatchDog/Stop` → `StartWatchDogStop`

## Error Handling

### Parse Failures

```csharp
var result = parser.DiscoverSchemas(schemas);

if (!result.Success)
{
    Console.WriteLine($"Parse failed: {result.ErrorMessage}");
    if (result.Exception != null)
    {
        Console.WriteLine($"Exception: {result.Exception}");
    }
}
```

### Timeout Detection

ETL parser automatically stops if no events received for configured timeout period (default: 10 seconds).

## Dependencies

- **Microsoft.Diagnostics.Tracing.TraceEvent** (3.1.13): ETL parsing engine
- **evtx** (1.2.0): EVTX file parsing
- **System.Diagnostics.EventLog** (8.0.1): Windows event log integration

## Target Framework

- .NET 8.0

## License

Copyright (c) Microsoft Corporation. All rights reserved.

## Contributing

This library is designed to be extensible. Implement `ITraceEventParser` for custom trace formats or `IEventExporter` for custom export targets.
