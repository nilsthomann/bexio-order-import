# run-coverage.ps1
# Runs all TUnit test projects with code coverage, merges the coverage and opens the HTML report

param (
    [string]$Configuration = "Debug",
    [switch]$CI
)

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$testResultsDir = Join-Path $rootDir "TestResults"
$coverageFile = Join-Path $testResultsDir "coverage.cobertura.xml"
$coverageReportDir = Join-Path $testResultsDir "CoverageReport"

# The host tests and one test project per import format
$testProjects = @(
    "BexioOrderImport.Tests",
    "BexioOrderImport.Format.Peakmile.Tests",
    "BexioOrderImport.Format.EanList.Tests",
    "BexioOrderImport.Format.Sgd.Tests"
)

Write-Host "1. Cleaning previous test results..." -ForegroundColor Cyan
if (Test-Path $testResultsDir) {
    Remove-Item -Recurse -Force $testResultsDir
}
New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null

Write-Host "2. Running TUnit tests and collecting coverage..." -ForegroundColor Cyan
$projectCoverageFiles = @()
foreach ($project in $testProjects) {
    Write-Host "   $project" -ForegroundColor Cyan
    $testProjPath = Join-Path $rootDir "src\$project\$project.csproj"
    $projectCoverageFile = Join-Path $testResultsDir "coverage.$project.cobertura.xml"
    dotnet run --project $testProjPath -c $Configuration -- --results-directory $testResultsDir --coverage --coverage-output-format cobertura --coverage-output $projectCoverageFile --report-trx --report-trx-filename "test-results.$project.trx"

    if (-not (Test-Path $projectCoverageFile)) {
        Write-Error "No coverage file found at $projectCoverageFile!"
        exit 1
    }
    $projectCoverageFiles += $projectCoverageFile
}

Write-Host "3. Merging coverage into $coverageFile..." -ForegroundColor Cyan
dotnet tool restore | Out-Null
$mergedDir = Join-Path $testResultsDir "Merged"
dotnet reportgenerator "-reports:$($projectCoverageFiles -join ';')" "-targetdir:$mergedDir" "-reporttypes:Cobertura"
Move-Item (Join-Path $mergedDir "Cobertura.xml") $coverageFile -Force

Write-Host "Found coverage file: $coverageFile" -ForegroundColor Green

if (-not ($CI -or $env:GITHUB_ACTIONS)) {
    Write-Host "4. Generating detailed ReportGenerator HTML coverage report..." -ForegroundColor Cyan
    dotnet reportgenerator "-reports:$coverageFile" "-targetdir:$coverageReportDir" "-reporttypes:Html"

    $indexHtml = Join-Path $coverageReportDir "index.html"
    if (Test-Path $indexHtml) {
        Write-Host "5. Opening coverage report in default browser: $indexHtml" -ForegroundColor Green
        Start-Process $indexHtml
    }
}
