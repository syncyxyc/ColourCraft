# Gemeinsame Hilfsfunktionen für die Build-Skripte.
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'OrbitStudio.sln'
$Artifacts = Join-Path $RepoRoot 'artifacts'

function Get-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $fallback = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $fallback) { return $fallback }
    throw '.NET SDK nicht gefunden. Bitte .NET 10 SDK installieren (winget install Microsoft.DotNet.SDK.10).'
}

# Ohne param-Block: alle Argumente (auch "-c", "-r", "-p:...") werden unverändert an dotnet durchgereicht.
function Invoke-Dotnet {
    $dotnet = Get-Dotnet
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') ist fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}
