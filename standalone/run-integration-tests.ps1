# Run Integration Tests Script for ASEventReader

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "ASEventReader Integration Tests" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to the standalone directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Check if test data directory exists
$testDataDir = "/mnt/X/icm/IL17"
if (Test-Path $testDataDir) {
    $etlFiles = Get-ChildItem -Path $testDataDir -Filter "*.etl" -Recurse -ErrorAction SilentlyContinue
    $zipFiles = Get-ChildItem -Path $testDataDir -Filter "*.zip" -Recurse -ErrorAction SilentlyContinue
    $totalFiles = $etlFiles.Count + $zipFiles.Count

    Write-Host "Test data directory: $testDataDir" -ForegroundColor Green
    Write-Host "  ETL files found: $($etlFiles.Count)" -ForegroundColor Gray
    Write-Host "  ZIP files found: $($zipFiles.Count)" -ForegroundColor Gray
    Write-Host "  Total files: $totalFiles" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "WARNING: Test data directory not found: $testDataDir" -ForegroundColor Yellow
    Write-Host "Integration tests may be inconclusive." -ForegroundColor Yellow
    Write-Host ""
}

# Build the solution first
Write-Host "Building the solution..." -ForegroundColor Yellow
dotnet build --nologo --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Run integration tests
Write-Host "Running integration tests..." -ForegroundColor Yellow
Write-Host "This may take several minutes depending on file sizes..." -ForegroundColor Gray
Write-Host ""

dotnet test --nologo --no-build --filter "TestCategory=Integration" --verbosity normal

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Integration tests failed or were inconclusive" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  - Test data directory doesn't exist: $testDataDir" -ForegroundColor Gray
    Write-Host "  - No ETL/ZIP files in the directory" -ForegroundColor Gray
    Write-Host "  - Permission denied accessing files" -ForegroundColor Gray
    Write-Host "  - Files are corrupted or invalid" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Integration tests completed!" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
