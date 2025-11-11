# EtwEventReader Integration Tests

Integration tests for the EtwEventReader project that test against real ETL and ZIP files.

## Overview

This project contains integration tests that read actual ETL/ZIP files from a specified directory and analyze their contents. Unlike unit tests, these tests interact with real file system and process actual trace data.

## Test Data Location

The integration tests read files from: `/mnt/X/icm/IL17`

This directory should contain:
- `.etl` files (Event Trace Log files)
- `.zip` files (compressed ETL files)

## Test Categories

Tests in this project are tagged with the `[TestCategory("Integration")]` attribute.

## Running the Integration Tests

### Run All Tests (Including Integration Tests)

```bash
cd /mnt/e/work/hub/common/tools/src/standalone
dotnet test
```

### Run Only Integration Tests

```bash
# Filter by test category
dotnet test --filter TestCategory=Integration

# Or filter by project
dotnet test tests/EtwEventReaderIntegrationTests/EtwEventReaderIntegrationTests.csproj
```

### Run Specific Test

```bash
dotnet test --filter FullyQualifiedName~ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics
```

## Test Output

The integration tests produce detailed console output including:

### Per-File Statistics
- File name and path
- File size
- Total event count
- List of providers with event counts and percentages
- Top event types per provider with counts and percentages

### Overall Summary
- Total files processed
- Total events across all files
- Average events per file
- Unique providers across all files
- Top 10 providers by event count with statistics

### Example Output

```
================================================================================
ETL/ZIP File Analysis from: /mnt/X/icm/IL17
================================================================================
Total files found: 5 (ETL: 3, ZIP: 2)

Processing: trace001.etl
  Path: /mnt/X/icm/IL17/trace001.etl
  Size: 45.23 MB
  Total Events: 125,487
  Unique Providers: 15

  Provider Statistics:
  ----------------------------------------------------------------------------
  • Microsoft-Windows-Kernel-Process
    Events: 45,123 (35.96%)
    Event Types: 12
      - ProcessStart: 2,345 (5.20%)
      - ProcessStop: 2,340 (5.18%)
      - ThreadStart: 18,234 (40.41%)
      - ThreadStop: 18,230 (40.40%)
      - ImageLoad: 3,974 (8.81%)

  • Microsoft-Windows-NDIS-PacketCapture
    Events: 32,456 (25.87%)
    Event Types: 8
      - PacketFragment: 30,124 (92.81%)
      - PacketMetadata: 2,332 (7.19%)

...

================================================================================
OVERALL SUMMARY
================================================================================

Files Processed: 5
Total Events: 542,318
Average Events per File: 108,463

Unique Providers Across All Files: 23

Top 10 Providers by Event Count:
--------------------------------------------------------------------------------
• Microsoft-Windows-Kernel-Process
  Total Events: 201,456 (37.13%)
  Files: 5
  Unique Event Types: 15

• Microsoft-Windows-NDIS-PacketCapture
  Total Events: 145,678 (26.86%)
  Files: 3
  Unique Event Types: 10

...

================================================================================
```

## Test Details

### ETLFileReaderIntegrationTests

#### ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics

**Purpose**: Reads all ETL and ZIP files from the test directory and prints comprehensive statistics.

**What it does**:
1. Scans `/mnt/X/icm/IL17` for `.etl` and `.zip` files
2. Processes each file using EventProcessor
3. Analyzes events by provider and event type
4. Prints detailed statistics for each file
5. Prints overall summary across all files

**Assertions**:
- At least one file was successfully processed
- Directory exists (or test is inconclusive)

#### ReadETLFiles_WithProviderFilter_ReturnsFilteredEvents

**Purpose**: Tests provider filtering functionality.

**What it does**:
1. Reads the first ETL file from the directory
2. Extracts a provider name from the events
3. Re-reads the file with provider filter applied
4. Compares filtered vs. unfiltered event counts

**Assertions**:
- Filtered events contain at least one event
- Test data directory exists (or test is inconclusive)

## Troubleshooting

### Test Directory Not Found

If you see:
```
Test 'ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics' was inconclusive
Message: Test data directory does not exist: /mnt/X/icm/IL17
```

**Solution**: Ensure the directory `/mnt/X/icm/IL17` exists and is accessible. Update the `TestDataDirectory` constant in `ETLFileReaderIntegrationTests.cs` if your test files are in a different location.

### No Files Found

If you see:
```
Message: No ETL or ZIP files found in: /mnt/X/icm/IL17
```

**Solution**: Place some `.etl` or `.zip` files in the test directory.

### Permission Denied

If you encounter permission errors:
```
ERROR: Access to the path '/mnt/X/icm/IL17/file.etl' is denied.
```

**Solution**: Ensure you have read permissions on the directory and files.

### File Processing Errors

Individual file errors are caught and displayed but don't fail the entire test. Check the console output for specific file errors.

## Test Configuration

### Changing Test Data Location

Edit the constant in `ETLFileReaderIntegrationTests.cs`:

```csharp
private const string TestDataDirectory = "/mnt/X/icm/IL17";
```

Change this to point to your test data location.

### Filtering Files

The tests process all `.etl` and `.zip` files recursively. To limit:

```csharp
// Only top-level directory
var etlFiles = Directory.GetFiles(TestDataDirectory, "*.etl", SearchOption.TopDirectoryOnly);

// Only first N files
var allFiles = etlFiles.Concat(zipFiles).Take(5).ToArray();
```

## Performance Considerations

- Large ETL files can take significant time to process
- ZIP files are extracted to temporary directories
- Integration tests may take several minutes depending on file sizes
- Each file is processed sequentially

## Output Verbosity

To see detailed console output when running tests:

```bash
# Detailed output
dotnet test --verbosity detailed

# Normal output (shows Console.WriteLine)
dotnet test --verbosity normal

# Minimal output
dotnet test --verbosity minimal
```

## CI/CD Integration

For continuous integration:

```bash
# Skip integration tests in CI (if test data not available)
dotnet test --filter "TestCategory!=Integration"

# Run only integration tests in dedicated CI job
dotnet test --filter "TestCategory=Integration" --logger "trx;LogFileName=integration-test-results.trx"
```

## Dependencies

- **EtwEventReader** - Main project (project reference)
- **Microsoft.NET.Test.Sdk** - Test SDK
- **MSTest.TestAdapter** - MSTest test adapter
- **MSTest.TestFramework** - MSTest framework
- **coverlet.collector** - Code coverage

## Project Structure

```
EtwEventReaderIntegrationTests/
├── EtwEventReaderIntegrationTests.csproj  # Project file (net8.0)
├── README.md                              # This file
└── ETLFileReaderIntegrationTests.cs      # Main integration test class
    ├── FileStatistics (private class)
    ├── ProviderStatistics (private class)
    └── EventTypeStatistics (private class)
```

## Future Enhancements

Potential additions to the integration test suite:
- Performance benchmarking tests
- Memory usage tests
- Large file handling tests
- Concurrent file processing tests
- Specific provider/event filtering tests
- Error scenario tests (corrupted files, etc.)
