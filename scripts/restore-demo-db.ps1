param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$BackupPath,
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupPath)) {
    throw "Không tìm thấy file backup: $BackupPath"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appsettings = Get-Content -Raw (Join-Path $repoRoot "WebHomestay\appsettings.json") | ConvertFrom-Json
$connectionString = [string]$appsettings.ConnectionStrings.DefaultConnection
$parts = @{}
foreach ($part in $connectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
    $segments = $part.Split('=', 2)
    if ($segments.Length -eq 2) {
        $parts[$segments[0]] = $segments[1]
    }
}

$env:PGPASSWORD = $parts["Password"]
$psqlExe = Join-Path $PgBin "psql.exe"
$pgRestoreExe = Join-Path $PgBin "pg_restore.exe"

& $psqlExe -h $parts["Host"] -p $parts["Port"] -U $parts["Username"] -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();"
& $psqlExe -h $parts["Host"] -p $parts["Port"] -U $parts["Username"] -d postgres -c "DROP DATABASE IF EXISTS $DatabaseName;"
& $psqlExe -h $parts["Host"] -p $parts["Port"] -U $parts["Username"] -d postgres -c "CREATE DATABASE $DatabaseName;"
& $pgRestoreExe -h $parts["Host"] -p $parts["Port"] -U $parts["Username"] -d $DatabaseName --clean --if-exists --no-owner $BackupPath

Write-Host "Restore completed for $DatabaseName"
