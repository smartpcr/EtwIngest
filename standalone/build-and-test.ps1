# Build and Test Script for ASEventReader

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "ASEventReader Build and Test Script" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to the standalone directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray
Write-Host ""

# Check .NET version
Write-Host "Checking .NET version..." -ForegroundColor Yellow
dotnet --version
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean --nologo
Write-Host ""

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: NuGet restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Restore successful" -ForegroundColor Green
Write-Host ""

# Build the solution
Write-Host "Building the solution..." -ForegroundColor Yellow
dotnet build --nologo --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Run unit tests
Write-Host "Running unit tests..." -ForegroundColor Yellow
dotnet test --nologo --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Tests failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ All tests passed" -ForegroundColor Green
Write-Host ""

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Build and test completed successfully!" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
