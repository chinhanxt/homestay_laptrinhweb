param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin",
    [string]$BackupDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($BackupDir)) {
    $BackupDir = Join-Path $repoRoot "backups\demo"
}

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
$pgDumpExe = Join-Path $PgBin "pg_dump.exe"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupDir "$DatabaseName-demo-$timestamp.dump"

& $pgDumpExe -h $parts["Host"] -p $parts["Port"] -U $parts["Username"] -d $DatabaseName -F c -f $backupPath

Write-Host $backupPath
