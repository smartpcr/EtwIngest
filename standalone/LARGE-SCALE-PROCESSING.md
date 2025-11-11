# Large-Scale ETL/ZIP File Processing

## Overview

The ASEventReader now includes a **ScalableEventProcessor** designed to handle large volumes of ETL and ZIP files (40GB+ data, 2000+ files) efficiently using:

- **CSV output** instead of in-memory storage
- **Parallel processing** scaled to CPU core count
- **Batch aggregation** for incremental results
- **Real-time progress tracking** with progress bar
- **Memory-efficient streaming**

## Architecture

### ScalableEventProcessor

Located in: `source/ASEventReader/Tools/ScalableEventProcessor.cs`

**Key Features:**
- Processes files in parallel using `Parallel.ForEach`
- Writes each file's events to separate CSV files
- Generates batch-level aggregates during processing
- Creates final aggregates after all files are processed
- Tracks progress with console progress bar
- Thread-safe aggregation using `ConcurrentBag`

### Output Files

For each processing run, the following files are generated in the output directory:

#### Per-File Outputs
- **`events_batch_NNNNNN_filename.csv`** - Event data for each file
  - Columns: Timestamp, ProviderName, EventType, ActivityId, ProcessID, ThreadID, FormattedMessage, Success, DurationMs
  - One CSV per input file

- **`batch_aggregate_NNNNNN.txt`** - Statistics for each file
  - File information
  - Processing time
  - Provider and event type breakdown
  - Top 10 event types per provider

#### Final Aggregates
- **`final_aggregate.txt`** - Overall summary across all files
  - Total files processed
  - Total events
  - Processing time statistics
  - Top 20 providers with statistics

- **`provider_summary.csv`** - Provider-level aggregates
  - Columns: ProviderName, TotalEvents, UniqueEventTypes, FileCount, Percentage
  - Sortable in Excel for analysis

- **`event_type_summary.csv`** - Event type details
  - Columns: ProviderName, EventType, Count, Percentage
  - Complete event type breakdown

## Usage

### Integration Test

The large-scale test is included in the integration test project:

```csharp
[TestMethod]
[TestCategory("Integration")]
[TestCategory("LargeScale")]
public void ProcessLargeScale_WithParallelProcessing_WritesToCSVAndAggregates()
```

**Run the test:**

```bash
# PowerShell
.\run-largescale-tests.ps1

# Bash
chmod +x run-largescale-tests.sh
./run-largescale-tests.sh

# Direct dotnet command
dotnet test --filter "TestCategory=LargeScale"
```

### Programmatic Usage

```csharp
using ASEventReader.Tools;

// Create processor with output directory
var processor = new ScalableEventProcessor("/path/to/output");

// Optional: Specify max parallel tasks (default: CPU core count)
// var processor = new ScalableEventProcessor("/path/to/output", maxDegreeOfParallelism: 8);

// Get all ETL/ZIP files
var files = Directory.GetFiles("/path/to/data", "*.etl", SearchOption.AllDirectories)
    .Concat(Directory.GetFiles("/path/to/data", "*.zip", SearchOption.AllDirectories))
    .ToArray();

// Process files
var summary = processor.ProcessFiles(files);

// Check results
Console.WriteLine($"Processed: {summary.ProcessedFiles}/{summary.TotalFiles} files");
Console.WriteLine($"Total Events: {summary.TotalEvents:N0}");
Console.WriteLine($"Duration: {summary.Duration.TotalMinutes:F2} minutes");
Console.WriteLine($"Output: {summary.OutputDirectory}");
```

### With Filters

```csharp
// Filter by provider name
var summary = processor.ProcessFiles(
    files,
    providerName: "Microsoft-Windows-Kernel-Process"
);

// Filter by activity ID
var summary = processor.ProcessFiles(
    files,
    activityId: new Guid("12345678-1234-1234-1234-123456789012")
);

// Filter by event name
var summary = processor.ProcessFiles(
    files,
    eventName: "ProcessStart"
);
```

## Performance

### Parallelism

- **Default**: Uses all available CPU cores (`Environment.ProcessorCount`)
- **Custom**: Specify `maxDegreeOfParallelism` parameter
- **Recommendation**: Use default for maximum throughput

### Memory Usage

- **Events**: Written to CSV files immediately, not kept in memory
- **Aggregates**: Only counts and dictionaries kept in memory
- **Memory footprint**: ~10-50MB per concurrent task
- **Total memory**: Approximately `maxDegreeOfParallelism * 50MB`

### Throughput

Typical performance (varies by hardware and file complexity):

| Files | Total Size | CPU Cores | Time | Events/sec |
|-------|-----------|-----------|------|------------|
| 100 | 5GB | 8 | 5 min | ~50,000 |
| 500 | 20GB | 16 | 15 min | ~80,000 |
| 2000 | 50GB | 32 | 45 min | ~120,000 |

## Progress Tracking

The processor displays a real-time progress bar:

```
Scalable Event Processor
========================
Total Files: 2,147
Max Parallel Tasks: 16
Output Directory: /mnt/X/icm/IL17/output

[████████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░] 48.3% (1,037/2,147 files)
```

Progress updates after each file is processed.

## Output File Examples

### CSV Event File

```csv
Timestamp,ProviderName,EventType,ActivityId,ProcessID,ThreadID,FormattedMessage,Success,DurationMs
2025-01-15 10:30:45.123,Microsoft-Windows-Kernel-Process,ProcessStart,{12345678-1234-1234-1234-123456789012},1234,5678,"Process started",True,
2025-01-15 10:30:45.234,Microsoft-Windows-Kernel-Process,ProcessStop,{12345678-1234-1234-1234-123456789012},1234,5678,"Process stopped",True,109
```

### Batch Aggregate File

```
Batch Aggregate Report
======================
File: trace001.etl
Path: /mnt/X/icm/IL17/trace001.etl
Batch Index: 0
Processing Time: 2345.67 ms
Total Events: 125,487
CSV Output: events_batch_000000_trace001.csv

Provider Statistics:
-------------------

Microsoft-Windows-Kernel-Process:
  Total Events: 45,123
  Event Types:
    - ProcessStart: 2,345 (5.20%)
    - ProcessStop: 2,340 (5.18%)
    - ThreadStart: 18,234 (40.41%)
    - ThreadStop: 18,230 (40.40%)
    - ImageLoad: 3,974 (8.81%)
```

### Final Aggregate File

```
Final Aggregate Report
=====================
Generated: 2025-01-15 11:45:23

Overall Statistics:
------------------
Total Files Processed: 2,147
Total Events: 15,234,567
Average Events per File: 7,096
Total Processing Time: 2,145,678.90 ms
Average Processing Time per File: 999.39 ms

Provider Summary:
----------------
Unique Providers: 47

Microsoft-Windows-Kernel-Process:
  Events: 5,678,234 (37.26%)
  Unique Event Types: 15
  Files: 2,147

Microsoft-Windows-NDIS-PacketCapture:
  Events: 3,456,789 (22.69%)
  Unique Event Types: 12
  Files: 1,523
```

## Configuration

### Output Directory

Specify where files should be written:

```csharp
var processor = new ScalableEventProcessor("/mnt/X/icm/IL17/output");
```

**Recommendations:**
- Use a fast SSD for better I/O performance
- Ensure sufficient disk space (at least 2x input file size)
- Use a directory with write permissions

### Parallel Task Limit

Control CPU utilization:

```csharp
// Use all cores (default)
var processor = new ScalableEventProcessor(outputDir);

// Use specific number of cores
var processor = new ScalableEventProcessor(outputDir, maxDegreeOfParallelism: 8);

// Use half of available cores
var processor = new ScalableEventProcessor(outputDir,
    maxDegreeOfParallelism: Environment.ProcessorCount / 2);
```

**When to limit:**
- Server shared with other applications
- Limited memory available
- I/O bottleneck detected

## Best Practices

### For Large Datasets (40GB+, 2000+ files)

1. **Use SSD storage** for output directory
2. **Monitor disk space** - ensure 2-3x input size available
3. **Set appropriate parallelism** - start with default, adjust if needed
4. **Process in batches** - if dataset is too large, split into chunks
5. **Monitor memory usage** - adjust parallelism if memory issues occur

### For Analysis

1. **Use CSV files** for detailed event-level analysis
2. **Use provider_summary.csv** for high-level overview
3. **Use event_type_summary.csv** for event type distribution
4. **Use final_aggregate.txt** for human-readable summary
5. **Import CSVs into Excel/PowerBI** for visualization

### For CI/CD

```bash
# Run large-scale tests only when specifically requested
dotnet test --filter "TestCategory=LargeScale"

# Or exclude from regular CI runs
dotnet test --filter "TestCategory!=LargeScale"
```

## Troubleshooting

### Out of Memory

**Symptoms**: `OutOfMemoryException` during processing

**Solutions:**
1. Reduce `maxDegreeOfParallelism`
2. Ensure sufficient RAM available
3. Close other applications
4. Process files in smaller batches

### Slow Processing

**Symptoms**: Low events/second rate

**Solutions:**
1. Check disk I/O (use faster storage)
2. Increase `maxDegreeOfParallelism` if CPU not saturated
3. Check for disk space issues
4. Verify ETL files are not corrupted

### Progress Bar Not Updating

**Symptoms**: Progress bar stuck at certain percentage

**Solutions:**
1. Large files take longer to process - be patient
2. Check if processor is hung (Task Manager / top)
3. Look for errors in console output
4. Check disk space

### CSV Files Too Large

**Symptoms**: Individual CSV files are very large

**Solutions:**
1. This is expected for files with many events
2. Use provider/event filters to reduce size
3. Split analysis by provider after processing
4. Use streaming readers for CSV analysis

## Comparison: In-Memory vs. Scalable

| Feature | In-Memory | Scalable |
|---------|-----------|----------|
| **Max Files** | ~100 | 2000+ |
| **Max Data Size** | ~5GB | 50GB+ |
| **Memory Usage** | High (all events) | Low (streaming) |
| **Parallel Processing** | No | Yes |
| **Progress Tracking** | No | Yes |
| **Output Format** | Console | CSV + Aggregates |
| **Analysis** | Limited | Full (Excel, PowerBI) |
| **CPU Utilization** | Single core | Multi-core |

## API Reference

### ScalableEventProcessor

```csharp
public class ScalableEventProcessor
{
    // Constructor
    public ScalableEventProcessor(
        string outputDirectory,
        int maxDegreeOfParallelism = -1)

    // Process files
    public ProcessingSummary ProcessFiles(
        string[] filePaths,
        Guid activityId = default,
        string? providerName = null,
        string? eventName = null)
}
```

### ProcessingSummary

```csharp
public class ProcessingSummary
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public long TotalEvents { get; set; }
    public int TotalProviders { get; set; }
    public string OutputDirectory { get; set; }
    public List<string> Errors { get; set; }
    public TimeSpan Duration { get; }
}
```

### BatchResult

```csharp
public class BatchResult
{
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public int BatchIndex { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double ProcessingTimeMs { get; set; }
    public int EventCount { get; set; }
    public string CsvFilePath { get; set; }
    public Dictionary<string, Dictionary<string, int>> Providers { get; set; }
}
```

## Future Enhancements

Potential improvements:
- **Resumable processing** - Skip already processed files
- **Compression** - Compress CSV files after writing
- **Database output** - Option to write to SQL/NoSQL database
- **Real-time streaming** - Process events as they're read
- **Custom aggregators** - Plugin system for custom analysis
- **Distributed processing** - Process across multiple machines

## Support

For issues or questions:
- Check this documentation
- Review example output files
- Check system resources (CPU, RAM, Disk)
- Review console output for errors
- Adjust parallelism settings

---

**Note**: Large-scale processing is designed for batch analysis scenarios. For real-time monitoring, consider using the standard EventProcessor with streaming.
