# run-coverage.ps1
# Runs TUnit tests with code coverage and generates a local HTML report

param (
    [string]$Configuration = "Debug",
    [switch]$CI
)

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$testProjPath = Join-Path $rootDir "src\BexioOrderImport.Tests\BexioOrderImport.Tests.csproj"
$testResultsDir = Join-Path $rootDir "TestResults"
$coverageReportDir = Join-Path $testResultsDir "CoverageReport"
$coverageFile = Join-Path $testResultsDir "coverage.cobertura.xml"

Write-Host "1. Cleaning previous test results..." -ForegroundColor Cyan
if (Test-Path $testResultsDir) {
    Remove-Item -Recurse -Force $testResultsDir
}
New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null

Write-Host "2. Running TUnit unit tests and collecting coverage..." -ForegroundColor Cyan
dotnet run --project $testProjPath -c $Configuration -- --results-directory $testResultsDir --coverage --coverage-output-format cobertura --coverage-output $coverageFile --report-trx --report-trx-filename test-results.trx

if (-not (Test-Path $coverageFile)) {
    Write-Error "No coverage.cobertura.xml found at $coverageFile!"
    exit 1
}

Write-Host "Found coverage file: $coverageFile" -ForegroundColor Green

# Restore local tools
dotnet tool restore

$reportTypes = if ($CI -or $env:GITHUB_ACTIONS) { "Html;MarkdownSummary" } else { "Html" }

Write-Host "3. Generating HTML coverage report..." -ForegroundColor Cyan
dotnet reportgenerator "-reports:$coverageFile" "-targetdir:$coverageReportDir" "-reporttypes:$reportTypes" "-assemblyfilters:-BexioOrderImport.Wpf.Views*;-BexioOrderImport.Wpf.Converters*;-BexioOrderImport.Wpf.Helpers.WindowHelper*;-BexioOrderImport.Wpf.App*;-BexioOrderImport.Wpf.Resources*;-XamlGeneratedNamespace*"

$reportIndex = Join-Path $coverageReportDir "index.html"
if (Test-Path $reportIndex) {
    if (-not ($CI -or $env:GITHUB_ACTIONS)) {
        Write-Host "4. Opening coverage report in default browser..." -ForegroundColor Green
        Start-Process $reportIndex
    }
} else {
    Write-Error "Failed to generate coverage report index.html."
}
