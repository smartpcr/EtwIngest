# Integration Tests Summary

## Overview

A new integration test project has been added to the ASEventReader solution to test against real ETL and ZIP files.

## What Was Added

### 1. New Project: ASEventReaderIntegrationTests

**Location**: `tests/ASEventReaderIntegrationTests/`

**Files Created**:
- `ASEventReaderIntegrationTests.csproj` - Project file (net8.0, MSTest)
- `ETLFileReaderIntegrationTests.cs` - Main test class with 2 test methods
- `README.md` - Comprehensive documentation for integration tests

### 2. Test Class: ETLFileReaderIntegrationTests

**Test Methods**:

#### ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics
- Reads all ETL and ZIP files from `/mnt/X/icm/IL17`
- Processes each file and extracts event statistics
- Groups events by Provider Name and Event Type
- Prints detailed statistics including:
  - File information (name, path, size)
  - Total event count per file
  - Provider statistics with event counts and percentages
  - Top event types per provider
  - Overall summary across all files

#### ReadETLFiles_WithProviderFilter_ReturnsFilteredEvents
- Tests provider name filtering functionality
- Reads first ETL file and extracts a provider name
- Re-reads with provider filter applied
- Compares filtered vs unfiltered event counts

### 3. Scripts Added

#### run-integration-tests.ps1 (PowerShell)
- Checks if test data directory exists
- Counts ETL/ZIP files available
- Builds the solution
- Runs integration tests with detailed output
- Provides troubleshooting tips on failure

#### run-integration-tests.sh (Bash)
- Same functionality as PowerShell script
- For Linux/WSL environments
- Must be made executable: `chmod +x run-integration-tests.sh`

### 4. Solution File Updated

**ASEventReader.sln** now includes three projects:
1. ASEventReader (main application)
2. ASEventReaderUnitTests (unit tests)
3. ASEventReaderIntegrationTests (integration tests) **← NEW**

### 5. Documentation Updated

**README.md** updated with:
- Solution structure showing integration tests
- Running tests section split into Unit Tests and Integration Tests
- Instructions for running integration tests
- Link to integration tests documentation

## Test Data Location

**Directory**: `/mnt/X/icm/IL17`

The integration tests expect to find:
- `.etl` files (Event Trace Log files)
- `.zip` files (compressed ETL files)

Files are discovered recursively in subdirectories.

## Running Integration Tests

### Option 1: Using dotnet test directly

```bash
# From the standalone directory
cd /mnt/e/work/hub/common/tools/src/standalone

# Run only integration tests
dotnet test --filter "TestCategory=Integration"

# Run with detailed output
dotnet test --filter "TestCategory=Integration" --verbosity detailed
```

### Option 2: Using PowerShell script (Windows)

```powershell
cd E:\work\hub\common\tools\src\standalone
.\run-integration-tests.ps1
```

### Option 3: Using Bash script (WSL/Linux)

```bash
cd /mnt/e/work/hub/common/tools/src/standalone
chmod +x run-integration-tests.sh
./run-integration-tests.sh
```

### Option 4: Run all tests (unit + integration)

```bash
dotnet test
```

## Expected Output

When integration tests run successfully, you'll see output similar to:

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

... (more providers)

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

... (more providers)

================================================================================

Passed!  - Failed:     0, Passed:    2, Skipped:     0, Total:    2
```

## Test Results

### If Test Data Exists

```
Test Run Successful.
Total tests: 2
     Passed: 2
 Total time: 2.5 Minutes
```

### If Test Data Directory Doesn't Exist

```
Test 'ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics' was inconclusive
Message: Test data directory does not exist: /mnt/X/icm/IL17

Test Run Inconclusive.
Total tests: 2
     Inconclusive: 2
```

## Statistics Provided

The integration tests analyze and report:

### Per-File Statistics
- **File name and path**
- **File size** (formatted as KB, MB, GB)
- **Total event count**
- **Number of unique providers**
- **Provider breakdown** with:
  - Event count and percentage
  - Number of event types
  - Top 5 event types with counts and percentages

### Overall Summary
- **Total files processed**
- **Total events across all files**
- **Average events per file**
- **Unique providers across all files**
- **Top 10 providers** with:
  - Total event count and percentage
  - Number of files containing events from this provider
  - Number of unique event types

## Configuration

To change the test data directory, edit `ETLFileReaderIntegrationTests.cs`:

```csharp
private const string TestDataDirectory = "/mnt/X/icm/IL17";
```

Change this constant to point to your test data location.

## Troubleshooting

### Directory Not Found

**Problem**: Test data directory `/mnt/X/icm/IL17` doesn't exist

**Solution**:
- Verify the directory path
- Update the `TestDataDirectory` constant if files are elsewhere
- Ensure you have permissions to access the directory

### No Files Found

**Problem**: No ETL or ZIP files in the directory

**Solution**:
- Add some `.etl` or `.zip` files to the directory
- Check subdirectories (tests search recursively)

### Permission Denied

**Problem**: Can't read files in the directory

**Solution**:
- Check file permissions
- Run with appropriate user permissions
- Verify network drive is mounted (if applicable)

### File Processing Errors

**Problem**: Individual files fail to process

**Solution**:
- Check the console output for specific file errors
- Verify files are valid ETL/ZIP format
- Check if files are corrupted

## Integration with CI/CD

### Skip Integration Tests in CI

If test data isn't available in CI environment:

```bash
dotnet test --filter "TestCategory!=Integration"
```

### Run Integration Tests in Dedicated CI Job

```bash
dotnet test --filter "TestCategory=Integration" --logger "trx;LogFileName=integration-test-results.trx"
```

## Project Structure

```
tests/ASEventReaderIntegrationTests/
├── ASEventReaderIntegrationTests.csproj  # Project file (net8.0)
├── README.md                              # Detailed documentation
└── ETLFileReaderIntegrationTests.cs      # Test class
    ├── ReadETLFiles_FromTestDirectory_PrintsProviderAndEventStatistics()
    ├── ReadETLFiles_WithProviderFilter_ReturnsFilteredEvents()
    ├── FileStatistics (private class)
    ├── ProviderStatistics (private class)
    └── EventTypeStatistics (private class)
```

## Dependencies

Same as unit tests project:
- Microsoft.NET.Test.Sdk v17.8.0
- MSTest.TestAdapter v3.1.1
- MSTest.TestFramework v3.1.1
- coverlet.collector v6.0.0
- Project reference to ASEventReader

## Performance Considerations

- Integration tests can take several minutes to complete
- Processing time depends on:
  - Number of files
  - File sizes
  - Number of events per file
- ZIP files require extraction (uses temporary directories)
- Each file is processed sequentially

## Future Enhancements

Potential additions:
- Performance benchmarking tests
- Memory usage monitoring
- Concurrent file processing tests
- Error scenario tests (corrupted files, etc.)
- Specific provider/event filtering tests
- Comparison tests (before/after changes)

## Summary

The integration test project provides:
- ✅ Real-world testing with actual ETL files
- ✅ Comprehensive event statistics and analysis
- ✅ Provider and event type breakdown
- ✅ File-by-file and overall summaries
- ✅ Detailed console output for analysis
- ✅ Filter testing capabilities
- ✅ Easy-to-run scripts
- ✅ Full documentation

The tests are designed to be:
- **Informative**: Detailed output shows exactly what's in your ETL files
- **Flexible**: Easy to configure test data location
- **Robust**: Handles missing directories and files gracefully
- **Practical**: Provides real statistics useful for debugging and analysis
