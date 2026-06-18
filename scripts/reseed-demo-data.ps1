param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin",
    [switch]$RunBackup
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-AppConnectionString {
    param([string]$RepoRoot)
    $appsettingsPath = Join-Path $RepoRoot "WebHomestay\appsettings.json"
    $appsettings = Get-Content -Raw $appsettingsPath | ConvertFrom-Json
    return [string]$appsettings.ConnectionStrings.DefaultConnection
}

function Convert-ConnectionStringToMap {
    param([string]$ConnectionString)
    $map = @{}
    foreach ($part in $ConnectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $segments = $part.Split('=', 2)
        if ($segments.Length -eq 2) {
            $map[$segments[0]] = $segments[1]
        }
    }
    return $map
}

function Build-ConnectionString {
    param(
        [hashtable]$ConnectionMap,
        [string]$DatabaseName
    )
    return "Host=$($ConnectionMap['Host']);Port=$($ConnectionMap['Port']);Database=$DatabaseName;Username=$($ConnectionMap['Username']);Password=$($ConnectionMap['Password'])"
}

function Ensure-DatabaseExists {
    param(
        [string]$PsqlExe,
        [hashtable]$ConnectionMap,
        [string]$DatabaseName
    )

    $env:PGPASSWORD = $ConnectionMap["Password"]
    $checkSql = "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName';"
    $exists = & $PsqlExe -h $ConnectionMap["Host"] -p $ConnectionMap["Port"] -U $ConnectionMap["Username"] -d postgres -tAc $checkSql
    if (($exists | Out-String).Trim() -ne "1") {
        & $PsqlExe -h $ConnectionMap["Host"] -p $ConnectionMap["Port"] -U $ConnectionMap["Username"] -d postgres -c "CREATE DATABASE $DatabaseName;"
    }
}

function Ensure-Asset {
    param(
        [string]$Source,
        [string]$Destination
    )
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    Copy-Item $Source $Destination -Force
}

$repoRoot = Get-RepoRoot
$connectionMap = Convert-ConnectionStringToMap (Get-AppConnectionString -RepoRoot $repoRoot)
$connectionString = Build-ConnectionString -ConnectionMap $connectionMap -DatabaseName $DatabaseName
$psqlExe = Join-Path $PgBin "psql.exe"

Ensure-DatabaseExists -PsqlExe $psqlExe -ConnectionMap $connectionMap -DatabaseName $DatabaseName

dotnet ef database update --project (Join-Path $repoRoot "WebHomestay\WebHomestay.csproj") --connection $connectionString

Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\IDCards\demo-cccd-front.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\IDCards\demo-cccd-back.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\masked\idcards\demo-masked-front.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\masked\idcards\demo-masked-back.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\chuyenkhoan.jpg") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payments\demo-payment-bill.jpg")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payment-qr\demo-q1.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payment-qr\demo-q7.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payment-qr\demo-bd.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payment-qr\demo-dn.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\payment-qr\demo-dt.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\chuyenkhoan.jpg") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\Cancellations\confirmation\demo-confirmation.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\ma qr.png") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\Cancellations\refundQr\demo-refund-qr.png")

& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\seed-demo-core.sql")
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\seed-demo-bookings.sql")
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\seed-demo-cancellations.sql")
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\seed-demo-history.sql")
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\verify-demo-data.sql")

if ($RunBackup) {
    powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts\backup-demo-db.ps1") -DatabaseName $DatabaseName -PgBin $PgBin
}
