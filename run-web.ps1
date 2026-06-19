param (
    [ValidateSet("main", "demo")]
    [string]$Database = "main"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "WebHomestay\WebHomestay.csproj"

$connectionStrings = @{
    main = $null
    demo = "Host=localhost;Port=5432;Database=web_homestay_demo;Username=postgres;Password=1510"
}

function Stop-WebHomestayListener {
    $connections = Get-NetTCPConnection -State Listen -LocalPort 5000 -ErrorAction SilentlyContinue
    if (-not $connections) {
        return
    }

    $ownedProcessIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $ownedProcessIds) {
        if (-not $processId) {
            continue
        }

        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if (-not $process) {
            continue
        }

        $processPath = ""
        try {
            $processPath = $process.Path
        }
        catch {
            $processPath = ""
        }

        $isWebHomestayProcess = $process.ProcessName -eq "WebHomestay"
        $isProjectDotnet = $process.ProcessName -eq "dotnet" -and $processPath -like "*dotnet.exe"

        if ($isWebHomestayProcess -or $isProjectDotnet) {
            Write-Host "Stopping existing process on port 5000: $($process.ProcessName) ($processId)" -ForegroundColor Yellow
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

Stop-WebHomestayListener

if ($Database -eq "demo") {
    $env:ConnectionStrings__DefaultConnection = $connectionStrings.demo
    Write-Host "Running WebHomestay with DEMO DB on http://localhost:5000" -ForegroundColor Cyan
}
else {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Write-Host "Running WebHomestay with MAIN DB on http://localhost:5000" -ForegroundColor Cyan
}

dotnet run --project $projectPath
