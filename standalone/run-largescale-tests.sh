#!/bin/bash

# Run Large-Scale Integration Tests Script for ASEventReader

echo "======================================="
echo "ASEventReader Large-Scale Tests"
echo "======================================="
echo ""

# Navigate to the standalone directory
cd "$(dirname "$0")"

# Check if test data directory exists
TEST_DATA_DIR="/mnt/X/icm/IL17"
if [ -d "$TEST_DATA_DIR" ]; then
    ETL_COUNT=$(find "$TEST_DATA_DIR" -name "*.etl" -type f 2>/dev/null | wc -l)
    ZIP_COUNT=$(find "$TEST_DATA_DIR" -name "*.zip" -type f 2>/dev/null | wc -l)
    TOTAL_COUNT=$((ETL_COUNT + ZIP_COUNT))

    # Calculate total size
    TOTAL_SIZE_KB=$(find "$TEST_DATA_DIR" \( -name "*.etl" -o -name "*.zip" \) -type f -exec du -k {} + 2>/dev/null | awk '{sum+=$1} END {print sum}')
    TOTAL_SIZE_GB=$(echo "scale=2; $TOTAL_SIZE_KB / 1024 / 1024" | bc)

    echo "Test data directory: $TEST_DATA_DIR"
    echo "  ETL files found: $ETL_COUNT"
    echo "  ZIP files found: $ZIP_COUNT"
    echo "  Total files: $TOTAL_COUNT"
    echo "  Total size: ${TOTAL_SIZE_GB} GB"
    echo ""
else
    echo "WARNING: Test data directory not found: $TEST_DATA_DIR"
    echo "Large-scale tests will be inconclusive."
    echo ""
fi

# Display system information
echo "System Information:"
echo "  CPU Cores: $(nproc)"
if command -v free &> /dev/null; then
    FREE_MEM_GB=$(free -g | awk '/^Mem:/ {print $4}')
    echo "  Available Memory: ${FREE_MEM_GB} GB"
fi
echo ""

# Build the solution first
echo "Building the solution..."
dotnet build --nologo --no-restore
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo "✓ Build successful"
echo ""

# Run large-scale tests
echo "Running large-scale integration tests..."
echo "This may take a considerable time for large datasets..."
echo ""

START_TIME=$(date +%s)

dotnet test --nologo --no-build --filter "TestCategory=LargeScale" --verbosity normal

TEST_RESULT=$?
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))
DURATION_MINUTES=$(echo "scale=2; $DURATION / 60" | bc)

if [ $TEST_RESULT -ne 0 ]; then
    echo ""
    echo "ERROR: Large-scale tests failed or were inconclusive"
    echo ""
    echo "Common issues:"
    echo "  - Test data directory doesn't exist: $TEST_DATA_DIR"
    echo "  - No ETL/ZIP files in the directory"
    echo "  - Permission denied accessing files"
    echo "  - Insufficient memory for large files"
    echo "  - Files are corrupted or invalid"
    exit 1
fi

echo ""
echo "======================================="
echo "Large-scale tests completed!"
echo "Total execution time: ${DURATION_MINUTES} minutes"
echo "======================================="
echo ""

# Display output directory information
OUTPUT_DIR="${TEST_DATA_DIR}/output"
if [ -d "$OUTPUT_DIR" ]; then
    CSV_COUNT=$(find "$OUTPUT_DIR" -name "*.csv" -type f 2>/dev/null | wc -l)
    TXT_COUNT=$(find "$OUTPUT_DIR" -name "*.txt" -type f 2>/dev/null | wc -l)

    echo "Output Files:"
    echo "  Location: $OUTPUT_DIR"
    echo "  CSV files: $CSV_COUNT"
    echo "  TXT files: $TXT_COUNT"
    echo ""
    echo "Key output files:"
    echo "  - final_aggregate.txt    : Overall summary statistics"
    echo "  - provider_summary.csv   : Provider aggregates"
    echo "  - event_type_summary.csv : Event type details"
    echo "  - events_batch_*.csv     : Per-file event data"
    echo "  - batch_aggregate_*.txt  : Per-file statistics"
fi
