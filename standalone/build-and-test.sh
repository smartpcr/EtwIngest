#!/bin/bash

# Build and Test Script for ASEventReader

echo "======================================="
echo "ASEventReader Build and Test Script"
echo "======================================="
echo ""

# Navigate to the standalone directory
cd "$(dirname "$0")"

echo "Working directory: $(pwd)"
echo ""

# Check .NET version
echo "Checking .NET version..."
dotnet --version
echo ""

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean --nologo
echo ""

# Restore NuGet packages
echo "Restoring NuGet packages..."
dotnet restore --nologo
if [ $? -ne 0 ]; then
    echo "ERROR: NuGet restore failed"
    exit 1
fi
echo "✓ Restore successful"
echo ""

# Build the solution
echo "Building the solution..."
dotnet build --nologo --no-restore
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo "✓ Build successful"
echo ""

# Run unit tests
echo "Running unit tests..."
dotnet test --nologo --no-build --verbosity normal
if [ $? -ne 0 ]; then
    echo "ERROR: Tests failed"
    exit 1
fi
echo "✓ All tests passed"
echo ""

echo "======================================="
echo "Build and test completed successfully!"
echo "======================================="
