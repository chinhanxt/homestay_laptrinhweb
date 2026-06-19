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

function New-DemoMaskedIdCardAsset {
    param(
        [string]$Source,
        [string]$Destination,
        [ValidateSet("Front", "Back")]
        [string]$Side
    )

    Add-Type -AssemblyName System.Drawing

    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null

    $bitmap = [System.Drawing.Bitmap]::new($Source)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

            $maskBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235, 0, 0, 0))
            $labelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 255, 255, 255))
            $bannerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(220, 189, 147, 75))
            $font = [System.Drawing.Font]::new("Arial", [math]::Max(14, [int]($bitmap.Width * 0.035)), [System.Drawing.FontStyle]::Bold)
            $labelFont = [System.Drawing.Font]::new("Arial", [math]::Max(11, [int]($bitmap.Width * 0.022)), [System.Drawing.FontStyle]::Bold)

            if ($Side -eq "Front") {
                $graphics.FillRectangle($maskBrush, [int]($bitmap.Width * 0.37), [int]($bitmap.Height * 0.22), [int]($bitmap.Width * 0.42), [int]($bitmap.Height * 0.11))
                $graphics.FillRectangle($maskBrush, [int]($bitmap.Width * 0.34), [int]($bitmap.Height * 0.56), [int]($bitmap.Width * 0.58), [int]($bitmap.Height * 0.26))
            }
            else {
                $graphics.FillRectangle($maskBrush, [int]($bitmap.Width * 0.70), [int]($bitmap.Height * 0.05), [int]($bitmap.Width * 0.20), [int]($bitmap.Height * 0.22))
                $graphics.FillRectangle($maskBrush, 0, [int]($bitmap.Height * 0.74), $bitmap.Width, [int]($bitmap.Height * 0.22))
            }

            $graphics.FillRectangle($bannerBrush, [int]($bitmap.Width * 0.03), [int]($bitmap.Height * 0.04), [int]($bitmap.Width * 0.30), [int]($bitmap.Height * 0.10))
            $graphics.DrawString("MASKED DEMO", $font, $labelBrush, [float]($bitmap.Width * 0.05), [float]($bitmap.Height * 0.055))
            $graphics.DrawString("BẢN CHE THÔNG TIN", $labelFont, $labelBrush, [float]($bitmap.Width * 0.05), [float]($bitmap.Height * 0.145))
        }
        finally {
            if ($graphics) { $graphics.Dispose() }
            if ($maskBrush) { $maskBrush.Dispose() }
            if ($labelBrush) { $labelBrush.Dispose() }
            if ($bannerBrush) { $bannerBrush.Dispose() }
            if ($font) { $font.Dispose() }
            if ($labelFont) { $labelFont.Dispose() }
        }

        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

$repoRoot = Get-RepoRoot
$connectionMap = Convert-ConnectionStringToMap (Get-AppConnectionString -RepoRoot $repoRoot)
$connectionString = Build-ConnectionString -ConnectionMap $connectionMap -DatabaseName $DatabaseName
$psqlExe = Join-Path $PgBin "psql.exe"

Ensure-DatabaseExists -PsqlExe $psqlExe -ConnectionMap $connectionMap -DatabaseName $DatabaseName

dotnet ef database update --project (Join-Path $repoRoot "WebHomestay\WebHomestay.csproj") --connection $connectionString

Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\IDCards\demo-cccd-front.png")
Ensure-Asset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\App_Data\SecureUploads\IDCards\demo-cccd-back.png")
New-DemoMaskedIdCardAsset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\masked\idcards\demo-masked-front.png") -Side Front
New-DemoMaskedIdCardAsset -Source (Join-Path $repoRoot "ảnh\cccd.png") -Destination (Join-Path $repoRoot "WebHomestay\wwwroot\uploads\masked\idcards\demo-masked-back.png") -Side Back
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
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\seed-demo-chat-history.sql")
& $psqlExe -h $connectionMap["Host"] -p $connectionMap["Port"] -U $connectionMap["Username"] -d $DatabaseName -v ON_ERROR_STOP=1 -f (Join-Path $repoRoot "scripts\verify-demo-data.sql")

if ($RunBackup) {
    powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot "scripts\backup-demo-db.ps1") -DatabaseName $DatabaseName -PgBin $PgBin
}
