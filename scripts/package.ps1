#requires -Version 7.0
<#
.SYNOPSIS
    Publishes a self-contained, single-file Windows build of RoboSync.
.DESCRIPTION
    Produces a standalone RoboSync.exe under .\publish that runs on any 64-bit Windows 10/11
    machine without a separate .NET installation.
.PARAMETER Runtime
    Target runtime identifier. Defaults to win-x64.
#>
param(
    [string]$Runtime = 'win-x64'
)
. "$PSScriptRoot/_dotnet.ps1"

$project = Join-Path $script:RepoRoot 'src/RoboSync.App/RoboSync.App.csproj'
$output = Join-Path $script:RepoRoot 'publish'

Write-Host "Publishing self-contained single-file build ($Runtime)..." -ForegroundColor Cyan
& $script:Dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $output

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDone. Standalone executable:" -ForegroundColor Green
    Write-Host (Join-Path $output 'RoboSync.App.exe')
}
exit $LASTEXITCODE
