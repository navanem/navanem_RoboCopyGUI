#requires -Version 7.0
<#
.SYNOPSIS
    Runs the test suite (unit + Robocopy integration tests).
#>
. "$PSScriptRoot/_dotnet.ps1"

$tests = Join-Path $script:RepoRoot 'tests/RoboSync.Core.Tests/RoboSync.Core.Tests.csproj'
Write-Host "Running tests..." -ForegroundColor Cyan
& $script:Dotnet test $tests -c Debug
exit $LASTEXITCODE
