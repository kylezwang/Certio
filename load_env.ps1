# PowerShell script to load environment variables
Write-Host "Loading environment variables from .env file..." -ForegroundColor Cyan

# Check if .env file exists
if (-not (Test-Path ".env")) {
    Write-Host "ERROR: .env file not found!" -ForegroundColor Red
    Write-Host "Please create a .env file with your environment variables." -ForegroundColor Yellow
    Read-Host "Press Enter to continue"
    exit 1
}

# Load environment variables from .env file
Get-Content ".env" | ForEach-Object {
    if ($_ -match "^([^#][^=]+)=(.*)$") {
        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        
        # Set environment variable for current session
        [Environment]::SetEnvironmentVariable($name, $value, "Process")
        Set-Variable -Name $name -Value $value -Scope Global
        
        Write-Host "Loaded: $name" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "✅ Environment variables loaded successfully!" -ForegroundColor Green
Write-Host ""

# Check Redis status
Write-Host "Checking Redis status..." -ForegroundColor Cyan
$redisContainer = docker ps --filter "name=redis-certio" --format "{{.Names}}" 2>$null
if ($redisContainer -eq "redis-certio") {
    Write-Host "✅ Redis container is running (localhost:6379)" -ForegroundColor Green
} else {
    Write-Host "⚠️  Redis container not running" -ForegroundColor Yellow
    Write-Host "   The app will fall back to in-memory cache." -ForegroundColor Yellow
    Write-Host "   To start Redis: docker run -d --name redis-certio -p 6379:6379 redis:latest" -ForegroundColor Gray
}
Write-Host ""

Write-Host "You can now use Docker commands with environment variables." -ForegroundColor Cyan
Write-Host "Example: docker exec -it certio-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost,1433 -U sa -P `$env:SQL_PASSWORD -C -N -W -s',' -Q `"SELECT Id, Title, Description, ConversationType, Status, CreatedAt, LastMessageAt FROM CertioLocal.dbo.Conversations ORDER BY CreatedAt DESC`"" -ForegroundColor Yellow
Write-Host ""
Read-Host "Press Enter to continue"
