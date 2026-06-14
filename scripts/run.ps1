#requires -Version 7.0
<#
.SYNOPSIS
    Builds and launches the RoboSync desktop application for local development.
#>
. "$PSScriptRoot/_dotnet.ps1"

Write-Host "Launching RoboSync (Debug)..." -ForegroundColor Cyan
& $script:Dotnet run --project (Join-Path $script:RepoRoot 'src/RoboSync.App/RoboSync.App.csproj')
