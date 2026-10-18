<#
.SYNOPSIS
    Veröffentlicht ORBIT Studio als eigenständige Single-File-EXE (win-x64, ohne separate .NET-Laufzeit).
    Ergebnis: artifacts/publish/OrbitStudio.exe
#>
. (Join-Path $PSScriptRoot 'common.ps1')

$output = Join-Path $Artifacts 'publish'
if (Test-Path $output) { Remove-Item $output -Recurse -Force }

Invoke-Dotnet publish (Join-Path $RepoRoot 'src/OrbitStudio.App/OrbitStudio.App.csproj') `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output

$exe = Join-Path $output 'OrbitStudio.exe'
if (-not (Test-Path $exe)) { throw "Veröffentlichung unvollständig: $exe fehlt." }
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Veröffentlicht: $exe ($sizeMb MB)" -ForegroundColor Green
