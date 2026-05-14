param (
    [string]$Action = "run"
)

$ProjectPath = "WebHomestay/WebHomestay.csproj"

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
        dotnet run --project $ProjectPath
    }
    "r" {
        dotnet run --project $ProjectPath
    }
    "watch" {
        dotnet watch --project $ProjectPath
    }
    "w" {
        dotnet watch --project $ProjectPath
    }
    "up" {
        dotnet restore $ProjectPath
        dotnet build $ProjectPath
        dotnet run --project $ProjectPath
    }
    "u" {
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
