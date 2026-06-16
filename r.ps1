param (
    [string]$Action = "run"
)

$ProjectPath = "WebHomestay/WebHomestay.csproj"

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

switch ($Action) {
    "restore" {
        dotnet restore $ProjectPath
    }
    "build" {
        dotnet build $ProjectPath
    }
    "b" {
        dotnet build $ProjectPath
    }
    "run" {
        Stop-WebHomestayListener
        dotnet run --project $ProjectPath
    }
    "r" {
        Stop-WebHomestayListener
        dotnet run --project $ProjectPath
    }
    "watch" {
        Stop-WebHomestayListener
        dotnet watch --project $ProjectPath
    }
    "w" {
        Stop-WebHomestayListener
        dotnet watch --project $ProjectPath
    }
    "up" {
        Stop-WebHomestayListener
        dotnet restore $ProjectPath
        dotnet build $ProjectPath
        dotnet run --project $ProjectPath
    }
    "u" {
        Stop-WebHomestayListener
        dotnet restore $ProjectPath
        dotnet build $ProjectPath
        dotnet run --project $ProjectPath
    }
    "clean" {
        dotnet clean $ProjectPath
        if (Test-Path "WebHomestay/bin") { Remove-Item -Recurse -Force "WebHomestay/bin" }
        if (Test-Path "WebHomestay/obj") { Remove-Item -Recurse -Force "WebHomestay/obj" }
    }
    "c" {
        dotnet clean $ProjectPath
        if (Test-Path "WebHomestay/bin") { Remove-Item -Recurse -Force "WebHomestay/bin" }
        if (Test-Path "WebHomestay/obj") { Remove-Item -Recurse -Force "WebHomestay/obj" }
    }
    Default {
        Write-Host "Available actions: restore, build (b), run (r), watch (w), up (u), clean (c)" -ForegroundColor Cyan
        Write-Host "Usage: .\r.ps1 <action>"
    }
}
