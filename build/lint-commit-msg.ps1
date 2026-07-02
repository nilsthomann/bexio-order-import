# lint-commit-msg.ps1
# Git hook script to validate commit messages against Conventional Commits conventions

param(
    [string]$commitMsgFile
)

if (-not $commitMsgFile) {
    Write-Error "No commit message file path provided."
    exit 1
}

# In git, path might be relative to repo root
$resolvedPath = $commitMsgFile
if (-not (Test-Path $resolvedPath)) {
    # Try relative to current directory
    $resolvedPath = Join-Path (Get-Location) $commitMsgFile
}

if (-not (Test-Path $resolvedPath)) {
    Write-Error "Commit message file not found: $commitMsgFile (Resolved: $resolvedPath)"
    exit 1
}

$commitMsg = (Get-Content -Path $resolvedPath -Raw).Trim()

# Ignore merge commit messages or revert commit messages generated automatically
if ($commitMsg -like "Merge branch *" -or $commitMsg -like "Merge remote-tracking branch *") {
    Write-Host "Automatic merge commit detected. Skipping validation." -ForegroundColor Gray
    exit 0
}

# Conventional Commits regex pattern
$pattern = "^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(?:\([a-zA-Z0-9_\-\.]+\))?!?: .+$"

if ($commitMsg -notmatch $pattern) {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "[ERROR] COMMIT MSG LINT FAILED: Invalid format!" -ForegroundColor Red
    Write-Host "----------------------------------------------------------" -ForegroundColor Red
    Write-Host "Commit message was: '$commitMsg'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Your commit message must follow the Conventional Commits spec:" -ForegroundColor White
    Write-Host "  <type>(<scope>)?: <description>" -ForegroundColor Green
    Write-Host ""
    Write-Host "Allowed types:" -ForegroundColor White
    Write-Host "  feat     - New features" -ForegroundColor White
    Write-Host "  fix      - Bug fixes" -ForegroundColor White
    Write-Host "  docs     - Documentation changes" -ForegroundColor White
    Write-Host "  style    - Formatting, visual tweaks (no code changes)" -ForegroundColor White
    Write-Host "  refactor - Code restructuring without changes in behavior" -ForegroundColor White
    Write-Host "  perf     - Performance improvements" -ForegroundColor White
    Write-Host "  test     - Adding or correcting tests" -ForegroundColor White
    Write-Host "  build    - Build system or installer changes" -ForegroundColor White
    Write-Host "  ci       - CI pipeline workflows (GitHub Actions)" -ForegroundColor White
    Write-Host "  chore    - Housekeeping, dependencies, metadata" -ForegroundColor White
    Write-Host "  revert   - Reverting previous commits" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor White
    Write-Host "  feat(wpf): add bapi token visibility toggle" -ForegroundColor White
    Write-Host "  fix(excel): resolve matrix coordinate indexing" -ForegroundColor White
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host "[SUCCESS] Commit message format is valid." -ForegroundColor Green
exit 0
