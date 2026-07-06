# run-coverage.ps1
# Runs tests with code coverage and generates a local HTML report

param (
    [string]$Configuration = "Debug",
    [switch]$CI
)

$ErrorActionPreference = "Stop"

Write-Host "1. Cleaning previous test results..." -ForegroundColor Cyan
if (Test-Path "../TestResults") {
    Remove-Item -Recurse -Force "../TestResults"
}

Write-Host "2. Running unit tests and collecting coverage..." -ForegroundColor Cyan
dotnet test ../BexioOrderImport.slnx -c $Configuration --collect:"XPlat Code Coverage" --results-directory ../TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude=`"[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Views.*,[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Converters.*,[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Helpers.WindowHelper,[BexioOrderImport.Wpf]BexioOrderImport.Wpf.App,[BexioOrderImport.Wpf]BexioOrderImport.Wpf.Resources.*,[BexioOrderImport.Wpf]XamlGeneratedNamespace.GeneratedInternalTypeHelper`"

# Find the generated Cobertura XML file
$coverageFile = Get-ChildItem -Path "../TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($null -eq $coverageFile) {
    Write-Error "No coverage.cobertura.xml found! Make sure coverlet.collector is installed in the test project."
    exit 1
}

Write-Host "Found coverage file: $($coverageFile.FullName)" -ForegroundColor Green

# Restore local tools
dotnet tool restore

$reportTypes = if ($CI -or $env:GITHUB_ACTIONS) { "Html;MarkdownSummary" } else { "Html" }

Write-Host "3. Generating HTML coverage report..." -ForegroundColor Cyan
dotnet reportgenerator "-reports:$($coverageFile.FullName)" "-targetdir:../TestResults/CoverageReport" "-reporttypes:$reportTypes"

$reportIndex = "../TestResults/CoverageReport/index.html"
if (Test-Path $reportIndex) {
    if (-not ($CI -or $env:GITHUB_ACTIONS)) {
        Write-Host "4. Opening coverage report in default browser..." -ForegroundColor Green
        Start-Process $reportIndex
    }
} else {
    Write-Error "Failed to generate coverage report index.html."
}
