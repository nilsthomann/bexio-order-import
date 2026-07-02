# run-coverage.ps1
# Runs tests with code coverage and generates a local HTML report

$ErrorActionPreference = "Stop"

Write-Host "1. Cleaning previous test results..." -ForegroundColor Cyan
if (Test-Path "../TestResults") {
    Remove-Item -Recurse -Force "../TestResults"
}

Write-Host "2. Running unit tests and collecting coverage..." -ForegroundColor Cyan
dotnet test ../BexioOrderImport.slnx --collect:"XPlat Code Coverage" --results-directory ../TestResults /p:Exclude="[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Views.*%2C[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Converters.*%2C[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Helpers.WindowHelper%2C[BexioOrderImport.Wpf]BexioOrderImport.Wpf.App%2C[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Resources.*"

# Find the generated Cobertura XML file
$coverageFile = Get-ChildItem -Path "../TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($null -eq $coverageFile) {
    Write-Error "No coverage.cobertura.xml found! Make sure coverlet.collector is installed in the test project."
    exit 1
}

Write-Host "Found coverage file: $($coverageFile.FullName)" -ForegroundColor Green

# Check if reportgenerator is installed
$reportGeneratorInstalled = $false
try {
    # Check if installed globally
    $null = Get-Command "reportgenerator" -ErrorAction SilentlyContinue
    if ($?) {
        $reportGeneratorInstalled = $true
    }
} catch {}

if (-not $reportGeneratorInstalled) {
    Write-Host "ReportGenerator dotnet tool is not installed globally." -ForegroundColor Yellow
    Write-Host "Installing dotnet-reportgenerator-globaltool globally..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

Write-Host "3. Generating HTML coverage report..." -ForegroundColor Cyan
reportgenerator "-reports:$($coverageFile.FullName)" "-targetdir:../TestResults/CoverageReport" "-reporttypes:Html"

$reportIndex = "../TestResults/CoverageReport/index.html"
if (Test-Path $reportIndex) {
    Write-Host "4. Opening coverage report in default browser..." -ForegroundColor Green
    Start-Process $reportIndex
} else {
    Write-Error "Failed to generate coverage report index.html."
}
