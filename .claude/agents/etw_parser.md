# ETW Parser Agent Documentation

## Overview

This document describes the ETW (Event Tracing for Windows) parsing architecture and implementation used in this project. ETW event files (*.etl) contain binary trace data that must be parsed to extract structured events for analysis and ingestion.

## Parsing Architecture

### High-Level Design

The ETW parsing implementation follows a two-phase approach:

1. **Schema Discovery Phase**: Parse ETL files to discover all unique event types and infer their schemas
2. **Data Extraction Phase**: Re-parse ETL files to extract events as structured data (CSV format) based on discovered schemas

This design allows for dynamic table creation in Kusto before ingestion, ensuring tables match the actual event schemas found in the files.

## Core Components

### 1. EtlFile Class (`/EtwIngest/Libs/EtlFile.cs`)

The main ETL parsing engine that wraps the Microsoft.Diagnostics.Tracing.TraceEvent library.

#### Key Methods

**`Parse(ConcurrentDictionary<(string, string), EtwEvent> eventSchema, ref bool failed)`**
- **Purpose**: Discovers event schemas by parsing the ETL file once
- **Input**: Concurrent dictionary to populate with (ProviderName, EventName) → EtwEvent mappings
- **Output**: Populates dictionary with one representative event per unique provider/event combination
- **Timeout Protection**: Stops processing if no events received for 10 seconds (handles corrupted files)

**`Process(Dictionary<(string, string), EtwEvent> eventSchemas)`**
- **Purpose**: Extracts all events from ETL file as CSV-formatted strings
- **Input**: Dictionary of known event schemas (from Parse phase)
- **Output**: Dictionary of (ProviderName, EventName) → List of CSV rows
- **Features**:
  - Escapes special characters in string fields
  - Maintains field order matching schema
  - Handles null/missing payload values

#### Implementation Details

```csharp
// Core parsing loop using DynamicTraceEventParser
using var source = new ETWTraceEventSource(etlFile);
var parser = new DynamicTraceEventParser(source);

parser.All += traceEvent =>
{
    var providerName = traceEvent.ProviderName;
    var eventName = traceEvent.EventName;

    // Standard fields extracted from every event
    - TimeStamp (DateTime)
    - ProcessID (int)
    - ProcessName (string)
    - Level (int)
    - Opcode (int)
    - OpcodeName (string)

    // Dynamic payload fields (varies by event type)
    foreach (var payloadName in traceEvent.PayloadNames)
    {
        var value = traceEvent.PayloadByName(payloadName);
        var type = value?.GetType() ?? typeof(string);
    }
};

source.Process(); // Blocking call that processes entire file
```

#### Timeout Mechanism

Both Parse() and Process() implement a watchdog timer:

```csharp
var timer = new System.Timers.Timer(10000); // 10 seconds
timer.Elapsed += (sender, e) =>
{
    if ((DateTime.UtcNow - lastEventTime).TotalSeconds >= 10)
    {
        source.StopProcessing();
        timer.Stop();
    }
};
```

This prevents the parser from hanging on:
- Corrupted ETL files
- Incomplete trace sessions
- Files with locked handles

### 2. EtwEvent Class (`/EtwIngest/Libs/EtwEvent.cs`)

Data structure representing a single ETW event type.

```csharp
public class EtwEvent
{
    public string ProviderName { get; set; }
    public string EventName { get; set; }
    public List<(string fieldName, Type fieldType)> PayloadSchema { get; set; }
    public Dictionary<string, object> Payload { get; set; }
}
```

**PayloadSchema**: Ordered list of field definitions
- Preserves field order for CSV column generation
- Maps .NET CLR types to Kusto types via `KustoExtension.ToKustoColumnType()`

**Payload**: Sample values from one representative event
- Used for schema inference only
- Not used during bulk extraction

### 3. KustoExtension Class (`/EtwIngest/Libs/KustoExtension.cs`)

Provides Kusto integration utilities for ETW data.

#### Type Mapping

```csharp
public static string ToKustoColumnType(this Type type)
{
    // CLR Type → Kusto Type
    typeof(string) → "string"
    typeof(DateTime) → "datetime"
    typeof(int) → "int"
    typeof(long) → "long"
    typeof(decimal/float/double) → "real"
    typeof(bool) → "bool"
    typeof(Guid) → "guid"
    typeof(TimeSpan) → "timespan"
    Enum types → "string"
    Non-scalar types → "dynamic"
}
```

#### DDL Generation

**`GenerateCreateTableCommand(string tableName, List<(string, Type)> fields)`**
- Creates Kusto table DDL from event schema
- Example output:
```kql
.create table ['ETL-ProviderName.EventName'] (
  TimeStamp : datetime,
  ProcessID : int,
  ProcessName : string,
  Level : int,
  Opcode : int,
  OpcodeName : string,
  customField1 : string,
  customField2 : long
)
```

**`GenerateCsvIngestionMapping(string tableName, string mappingName, List<(string, Type)> fields)`**
- Creates CSV ingestion mapping for Kusto
- Maps ordinal column positions to table columns
- Example output:
```kql
.create-or-alter table ['TableName'] ingestion csv mapping 'CsvMapping' '[
  {"column":"TimeStamp","datatype":"datetime","Ordinal":0},
  {"column":"ProcessID","datatype":"int","Ordinal":1},
  ...
]'
```

## Batch Processing Pipeline (EtlIterator)

The `EtlIterator/Program.cs` demonstrates production-scale ETL processing.

### Processing Flow

```
1. Discovery Phase (Parallel)
   ├─ Parse each ETL file
   ├─ Collect unique event schemas in concurrent dictionary
   └─ Track corrupted/failed files

2. Table Creation Phase (Parallel)
   ├─ Generate Kusto DDL for each unique event type
   ├─ Create tables if they don't exist
   └─ Create CSV ingestion mappings

3. CSV Generation Phase (Parallel)
   ├─ Create CSV files with headers
   ├─ Process each ETL file
   ├─ Extract events to CSV rows
   └─ Append to appropriate CSV files

4. Ingestion Phase
   ├─ Ingest CSV files into Kusto tables
   └─ Verify record counts
```

### Parallelization Strategy

```csharp
var parallelOptions = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

Parallel.ForEach(etlFiles, parallelOptions, etlFile =>
{
    var etl = new EtlFile(etlFile);
    etl.Parse(localEtwEvents, ref failedToParse);
    // Merge local results into global concurrent dictionary
});
```

### Memory Management

```csharp
// After each batch
allEtwEvents.Clear();
allEtwEvents = null;
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```

Aggressive garbage collection between batches to handle:
- Large numbers of ETL files
- Memory pressure from TraceEvent library
- Long-running processes

## Comparison: Collection vs. Parsing

### AzureStackDiagnostics Approach (Collection Only)

**Location**: `/mnt/e/work/hub/sln/deploy/src/AzureStackDiagnostics`

**Role**: Log collection infrastructure for Azure Stack diagnostics

**ETL Handling**:
- Treats *.etl files as opaque binary artifacts
- Collects based on date ranges (CreationTime, LastWriteTime)
- Zips large ETL files for transfer
- No parsing or schema inspection

**Code Example** (`LogCollectionHelper.psm1:968`):
```powershell
$allowedFileExtentions = '*.txt','*.log','*.etl','*.out','*.xml','*.bin',
                         '*.htm','*.html','*.mta','*.evtx','*.tsf','*.json',
                         '*.blg','*.zip','*.trace','*.csv','*.err','*.cab'

# ETL files filtered by date range
$items2 = Get-ChildItem -Path $Path -Include $ext -Recurse -Force
$items2 = $items2 | Where-Object {
    (($_.CreationTime -ge $FromDate) -or ($_.LastWriteTime -ge $FromDate)) -and
    $_.CreationTime -le $ToDate
}
```

**Use Cases**:
- Collecting logs from Azure Stack infrastructure
- Copying logs from VHDs (mounted disk images)
- Gathering Windows Event Logs (*.evtx) via WEvtUtil
- Transferring logs to central storage/Kusto staging

### EtwIngest Approach (Parsing & Ingestion)

**Location**: `/mnt/e/work/github/crp/EtwIngest`

**Role**: ETW/EVTX file parsing and Kusto ingestion pipeline

**ETL Handling**:
- Parses ETL files using Microsoft.Diagnostics.Tracing.TraceEvent
- Extracts structured event data with full schema inference
- Converts events to CSV for Kusto ingestion
- Creates Kusto tables dynamically based on discovered schemas

**Dependencies**:
- `Microsoft.Diagnostics.Tracing.TraceEvent` (3.1.13): ETL parsing engine
- `evtx` (1.2.0): Windows Event Log (.evtx) parsing
- `Microsoft.Azure.Kusto.Data` (12.2.7): Kusto integration

**Use Cases**:
- Analyzing ETW traces from production systems
- Ingesting trace data into Kusto for query/analysis
- Schema discovery for unknown event types
- Batch processing large trace collections

## Event Naming and Table Mapping

### Naming Convention

Kusto tables are named using the pattern:

```
ETL-{ProviderName}.{EventName}
```

Where:
- Provider names may contain hyphens, dots, and underscores
- Event names often contain slashes (e.g., "StartWatchDog/Stop")
- Slashes are removed from event names for table naming

### Examples

| Provider | Event | Table Name |
|----------|-------|------------|
| `MSNT_SystemTrace` | `EventTrace/PartitionInfoExtensionV2` | `ETL-MSNT_SystemTrace.EventTracePartitionInfoExtensionV2` |
| `Microsoft-AzureStack-Compute-HostPluginWatchDog` | `EnsureProcessStarted/Stop` | `ETL-Microsoft-AzureStack-Compute-HostPluginWatchDog.EnsureProcessStartedStop` |
| `Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin` | `OvsdbDbg` | `ETL-Microsoft.Windows.NetworkController.HostAgent.FirewallPlugin.OvsdbDbg` |

## CSV Export Format

### Special Character Handling

String fields containing special characters are escaped:

```csharp
if (fieldValue.Contains("\"") ||
    fieldValue.Contains(",") ||
    fieldValue.Contains(" ") ||
    fieldValue.Contains("\n") ||
    fieldValue.Contains("\r"))
{
    // 1. Escape internal quotes by doubling them
    string escapedField = fieldValue.Replace("\"", "\"\"");

    // 2. Wrap entire field in quotes
    rowBuilder.Append($"\"{escapedField}\"");
}
```

### Column Order

CSV columns follow fixed order:
1. Standard ETW fields (TimeStamp, ProcessID, ProcessName, Level, Opcode, OpcodeName)
2. Event-specific payload fields (in discovery order)

## Usage Patterns

### Single File Analysis (BDD Tests)

```csharp
// 1. Parse to discover schema
var etl = new EtlFile(etlFilePath);
var etwEvents = new ConcurrentDictionary<(string, string), EtwEvent>();
bool failed = false;
etl.Parse(etwEvents, ref failed);

// 2. Access specific event schema
var schema = etwEvents[("ProviderName", "EventName")].PayloadSchema;

// 3. Create Kusto table
var tableName = $"ETL-{providerName}.{eventName.Replace("/", "")}";
var createTableCmd = KustoExtension.GenerateCreateTableCommand(tableName, schema);
adminClient.ExecuteControlCommand(createTableCmd);

// 4. Extract to CSV
var csvData = etl.Process(etwEvents.ToDictionary(p => p.Key, p => p.Value));

// 5. Ingest into Kusto
var ingestCmd = $".ingest into table ['{tableName}'] (\"{csvFilePath}\") " +
                $"with (format='csv', ingestionMappingReference='CsvMapping', " +
                $"ignoreFirstRecord=true)";
```

### Batch Processing (Production)

```csharp
// 1. Parallel schema discovery
Parallel.ForEach(etlFiles, parallelOptions, etlFile =>
{
    var etl = new EtlFile(etlFile);
    etl.Parse(globalEventSchemas, ref failed);
});

// 2. Parallel table creation
Parallel.ForEach(globalEventSchemas.Keys, parallelOptions, key =>
{
    if (!adminClient.IsTableExist(kustoTableName))
    {
        // Create table and ingestion mapping
    }
});

// 3. Parallel CSV generation
Parallel.ForEach(etlFiles, parallelOptions, etlFile =>
{
    var etl = new EtlFile(etlFile);
    var csvData = etl.Process(globalEventSchemas);
    // Write to CSV files
});
```

## Error Handling

### Corrupted Files

- Timeout detection (10 second watchdog)
- Failed parse flag propagates to caller
- Corrupted files tracked separately
- Processing continues with remaining files

### Missing Event Data

- Null payload values handled gracefully
- Type inference falls back to `typeof(string)` for null values
- Missing fields in CSV rows left empty

### Kusto Integration

- Table existence checks before creation
- Ingestion mapping validation
- Record count verification after ingestion

## Performance Characteristics

### Single File Parsing

- **Throughput**: Depends on event density and file size
- **Timeout**: 10 seconds of inactivity triggers stop
- **Memory**: Holds one representative event per unique type

### Batch Processing

- **Parallelism**: `Environment.ProcessorCount` threads
- **Batch Size**: Configurable (default: 1 file per batch in EtlIterator)
- **Memory**: Explicit GC between batches
- **Progress**: Reports every 10 files processed

### Large File Handling

Example from BDD tests:
- Input: 745 unique event types from single ETL file
- Output: 88 CSV files (filtered by actual data presence)
- Tables: Automatically created in Kusto with correct schemas

## Future Enhancements

1. **Streaming CSV Generation**: Write CSV rows during Parse() instead of storing in memory
2. **Incremental Schema Discovery**: Cache known schemas to avoid full parsing
3. **Event Filtering**: Filter by provider/event during parsing (pre-extraction)
4. **Direct Kusto Ingestion**: Bypass CSV intermediate format using queued ingestion
5. **EVTX Integration**: Unified handling of ETL and EVTX files with common schema
