# Run Large-Scale Integration Tests Script for ASEventReader

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "ASEventReader Large-Scale Tests" -ForegroundColor Cyan
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

    # Calculate total size
    $totalSizeBytes = ($etlFiles + $zipFiles | Measure-Object -Property Length -Sum).Sum
    $totalSizeGB = $totalSizeBytes / 1GB

    Write-Host "Test data directory: $testDataDir" -ForegroundColor Green
    Write-Host "  ETL files found: $($etlFiles.Count)" -ForegroundColor Gray
    Write-Host "  ZIP files found: $($zipFiles.Count)" -ForegroundColor Gray
    Write-Host "  Total files: $totalFiles" -ForegroundColor Gray
    Write-Host "  Total size: $($totalSizeGB.ToString('F2')) GB" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "WARNING: Test data directory not found: $testDataDir" -ForegroundColor Yellow
    Write-Host "Large-scale tests will be inconclusive." -ForegroundColor Yellow
    Write-Host ""
}

# Display system information
Write-Host "System Information:" -ForegroundColor Yellow
Write-Host "  CPU Cores: $([Environment]::ProcessorCount)" -ForegroundColor Gray
Write-Host "  Available Memory: $([Math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)) GB" -ForegroundColor Gray
Write-Host ""

# Build the solution first
Write-Host "Building the solution..." -ForegroundColor Yellow
dotnet build --nologo --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Run large-scale tests
Write-Host "Running large-scale integration tests..." -ForegroundColor Yellow
Write-Host "This may take a considerable time for large datasets..." -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date

dotnet test --nologo --no-build --filter "TestCategory=LargeScale" --verbosity normal

$endTime = Get-Date
$duration = $endTime - $startTime

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Large-scale tests failed or were inconclusive" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  - Test data directory doesn't exist: $testDataDir" -ForegroundColor Gray
    Write-Host "  - No ETL/ZIP files in the directory" -ForegroundColor Gray
    Write-Host "  - Permission denied accessing files" -ForegroundColor Gray
    Write-Host "  - Insufficient memory for large files" -ForegroundColor Gray
    Write-Host "  - Files are corrupted or invalid" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Large-scale tests completed!" -ForegroundColor Cyan
Write-Host "Total execution time: $($duration.TotalMinutes.ToString('F2')) minutes" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Display output directory information
$outputDir = Join-Path $testDataDir "output"
if (Test-Path $outputDir) {
    $csvFiles = Get-ChildItem -Path $outputDir -Filter "*.csv" -ErrorAction SilentlyContinue
    $txtFiles = Get-ChildItem -Path $outputDir -Filter "*.txt" -ErrorAction SilentlyContinue

    Write-Host "Output Files:" -ForegroundColor Yellow
    Write-Host "  Location: $outputDir" -ForegroundColor Gray
    Write-Host "  CSV files: $($csvFiles.Count)" -ForegroundColor Gray
    Write-Host "  TXT files: $($txtFiles.Count)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Key output files:" -ForegroundColor Yellow
    Write-Host "  - final_aggregate.txt    : Overall summary statistics" -ForegroundColor Gray
    Write-Host "  - provider_summary.csv   : Provider aggregates" -ForegroundColor Gray
    Write-Host "  - event_type_summary.csv : Event type details" -ForegroundColor Gray
    Write-Host "  - events_batch_*.csv     : Per-file event data" -ForegroundColor Gray
    Write-Host "  - batch_aggregate_*.txt  : Per-file statistics" -ForegroundColor Gray
}
