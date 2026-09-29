# PowerShell CI/CD script to check for database files in git repository
# This script should be run in CI/CD pipelines to prevent database files from being committed

Write-Host "Checking for database files in repository..." -ForegroundColor Cyan

# Check for database files in git
$dbFiles = git ls-files | Select-String -Pattern '\.(db|db-shm|db-wal)$'

if ($dbFiles) {
    Write-Host " ERROR: Database files found in git repository!" -ForegroundColor Red
    Write-Host "The following database files are tracked in git:" -ForegroundColor Red
    $dbFiles | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Please remove these files from git using:" -ForegroundColor Yellow
    Write-Host "  git rm --cached <file>" -ForegroundColor White
    Write-Host "  git commit -m 'Remove database files from version control'" -ForegroundColor White
    exit 1
}

Write-Host " No database files found in repository" -ForegroundColor Green
exit 0

