#requires -Version 7.0
<#
.SYNOPSIS
    Restores and builds the full solution.
.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Release.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
. "$PSScriptRoot/_dotnet.ps1"

$solution = Join-Path $script:RepoRoot 'RoboSync.sln'
Write-Host "Building $solution ($Configuration)..." -ForegroundColor Cyan
& $script:Dotnet build $solution -c $Configuration
exit $LASTEXITCODE
