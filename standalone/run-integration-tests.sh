#!/bin/bash

# Run Integration Tests Script for ASEventReader

echo "======================================="
echo "ASEventReader Integration Tests"
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

    echo "Test data directory: $TEST_DATA_DIR"
    echo "  ETL files found: $ETL_COUNT"
    echo "  ZIP files found: $ZIP_COUNT"
    echo "  Total files: $TOTAL_COUNT"
    echo ""
else
    echo "WARNING: Test data directory not found: $TEST_DATA_DIR"
    echo "Integration tests may be inconclusive."
    echo ""
fi

# Build the solution first
echo "Building the solution..."
dotnet build --nologo --no-restore
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo "✓ Build successful"
echo ""

# Run integration tests
echo "Running integration tests..."
echo "This may take several minutes depending on file sizes..."
echo ""

dotnet test --nologo --no-build --filter "TestCategory=Integration" --verbosity normal

if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: Integration tests failed or were inconclusive"
    echo ""
    echo "Common issues:"
    echo "  - Test data directory doesn't exist: $TEST_DATA_DIR"
    echo "  - No ETL/ZIP files in the directory"
    echo "  - Permission denied accessing files"
    echo "  - Files are corrupted or invalid"
    exit 1
fi

echo ""
echo "======================================="
echo "Integration tests completed!"
echo "======================================="
