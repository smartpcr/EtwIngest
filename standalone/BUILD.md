# Build and Test Instructions for ASEventReader

## Prerequisites

- .NET 8.0 SDK or later
- Windows OS (required for TraceEvent library)

## Quick Start

### Option 1: Using PowerShell Script (Windows)

```powershell
.\build-and-test.ps1
```

### Option 2: Using Bash Script (WSL/Linux)

```bash
chmod +x build-and-test.sh
./build-and-test.sh
```

### Option 3: Manual Commands

```bash
# Navigate to the standalone directory
cd /mnt/e/work/hub/common/tools/src/standalone

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project source/ASEventReader -- --help
```

## Build Steps Explained

### 1. Restore NuGet Packages

```bash
dotnet restore
```

This command downloads all required NuGet packages:
- Microsoft.Diagnostics.Tracing.TraceEvent (v3.1.8) for the main project
- MSTest packages for the test project

### 2. Build the Solution

```bash
dotnet build
```

This compiles both projects:
- `source/ASEventReader/ASEventReader.csproj` - Main console application
- `tests/ASEventReaderUnitTests/ASEventReaderUnitTests.csproj` - Unit tests

Build output locations:
- Main project: `source/ASEventReader/bin/Debug/net8.0/ASEventReader.dll`
- Test project: `tests/ASEventReaderUnitTests/bin/Debug/net8.0/ASEventReaderUnitTests.dll`

### 3. Run Unit Tests

```bash
dotnet test
```

This runs all unit tests in the solution using MSTest framework.

Expected test output:
```
Passed!  - Failed:     0, Passed:    XX, Skipped:     0, Total:    XX
```

### 4. Run the Application

```bash
dotnet run --project source/ASEventReader -- <arguments>
```

Example:
```bash
dotnet run --project source/ASEventReader -- --help
dotnet run --project source/ASEventReader -- trace.etl
```

## Build in Visual Studio

1. Open `ASEventReader.sln` in Visual Studio 2022 or later
2. Select **Build > Build Solution** (or press Ctrl+Shift+B)
3. Run tests using **Test > Run All Tests**
4. Set `ASEventReader` as startup project and press F5 to run

## Troubleshooting

### Error: SDK 'Microsoft.NET.Sdk' not found

Ensure .NET 8.0 SDK is installed:
```bash
dotnet --version
```

Download from: https://dotnet.microsoft.com/download/dotnet/8.0

### Error: Package restore failed

Check internet connectivity and NuGet sources:
```bash
dotnet nuget list source
```

The project uses only nuget.org (configured in nuget.config).

### Error: Tests failed to run

Ensure the solution builds successfully first:
```bash
dotnet build
```

Then run tests with verbose output:
```bash
dotnet test --verbosity detailed
```

### Error: TraceEvent package not found

This package is only available on Windows. On Linux/Mac, some functionality may be limited.

## Clean Build

To perform a clean build:

```bash
# Clean previous build artifacts
dotnet clean

# Restore and rebuild
dotnet restore
dotnet build

# Run tests
dotnet test
```

## Build Configurations

The solution supports two build configurations:

- **Debug**: Default configuration with debugging symbols
- **Release**: Optimized configuration for deployment

```bash
# Build in Release mode
dotnet build -c Release

# Run tests in Release mode
dotnet test -c Release
```

## Continuous Integration

For CI/CD pipelines, use this command sequence:

```bash
dotnet restore --locked-mode
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --logger "trx;LogFileName=test-results.trx"
```

## Project Structure Verification

Verify all required files exist:

```
src/standalone/
├── ASEventReader.sln              ✓ Solution file
├── Directory.Build.props           ✓ Global properties
├── Directory.Build.targets         ✓ Global targets
├── nuget.config                    ✓ NuGet configuration
├── build-and-test.sh              ✓ Bash build script
├── build-and-test.ps1             ✓ PowerShell build script
├── source/ASEventReader/
│   ├── ASEventReader.csproj        ✓ Target: net8.0
│   ├── Program.cs                  ✓ Main entry point
│   ├── Models/*.cs                 ✓ 3 model files
│   ├── Tools/*.cs                  ✓ 3 tool files
│   └── EventFormatters/*.cs        ✓ 2 formatter files
└── tests/ASEventReaderUnitTests/
    ├── ASEventReaderUnitTests.csproj ✓ Target: net8.0
    ├── Models/*Tests.cs             ✓ 3 test files
    └── Tools/*Tests.cs              ✓ 2 test files
```

## Expected Test Results

The unit test project contains tests for:

### Models Tests
- **ASEventObjectTests**: 16 test methods
  - Constructor creates object with defaults
  - AddProperty adds properties successfully
  - AddProperty handles multiple properties
  - EventType property is set correctly
  - TimeStamp property is set correctly
  - DurationMs property is set correctly
  - ErrorMessage property is set correctly
  - Success returns true when no error message
  - Success returns false when error message exists
  - Success property can be set explicitly
  - AddChild sets hierarchy correctly
  - AddChild with multiple children updates IsLastSibling
  - ToString returns formatted output

- **PropertyNamesTests**: 3 test methods
  - Constants are defined correctly
  - AsProvidedProperties list is populated
  - AsProvidedProperties contains expected properties

- **EventNamesTests**: 1 test method
  - ManifestEventName constant is defined

### Tools Tests
- **EventProcessorTests**: 4 test methods
  - Constructor creates instance
  - GetEvents handles invalid path
  - GetEvents with empty array returns empty list
  - GetEvents handles null provider name filter

- **EtwScopeTrackerTests**: 3 test methods
  - Constructor creates instance
  - Tracker works with generic type

**Total Expected Tests**: ~27 tests

All tests should pass successfully.
