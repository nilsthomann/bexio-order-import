# run-coverage.ps1
# Runs TUnit tests with code coverage and opens the HTML report

param (
    [string]$Configuration = "Debug",
    [switch]$CI
)

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$testProjPath = Join-Path $rootDir "src\BexioOrderImport.Tests\BexioOrderImport.Tests.csproj"
$testResultsDir = Join-Path $rootDir "TestResults"
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

if (-not ($CI -or $env:GITHUB_ACTIONS)) {
    $htmlReport = Get-ChildItem -Path $testResultsDir -Filter "*.html" | Select-Object -First 1
    if ($htmlReport) {
        Write-Host "3. Opening coverage report in default browser: $($htmlReport.FullName)" -ForegroundColor Green
        Start-Process $htmlReport.FullName
    }
}
