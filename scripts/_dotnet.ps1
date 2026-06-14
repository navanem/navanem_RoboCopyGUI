# Resolves the dotnet executable. Prefers dotnet on PATH, then the default install location.
# Dot-source this file from other scripts: . "$PSScriptRoot/_dotnet.ps1"

function Get-DotnetPath {
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $default = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'
    if (Test-Path $default) { return $default }

    throw "The .NET SDK was not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download"
}

$script:Dotnet = Get-DotnetPath
$script:RepoRoot = Split-Path -Parent $PSScriptRoot
