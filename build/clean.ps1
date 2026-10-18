<#
.SYNOPSIS
    Entfernt alle Build-Ausgaben (bin, obj, artifacts). Quellcode bleibt unberührt.
#>
. (Join-Path $PSScriptRoot 'common.ps1')

$targets = Get-ChildItem $RepoRoot -Recurse -Directory -Include bin, obj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }
foreach ($dir in $targets) { Remove-Item $dir.FullName -Recurse -Force }
if (Test-Path $Artifacts) { Remove-Item $Artifacts -Recurse -Force }
Write-Host "Bereinigt: $($targets.Count) Ordner sowie artifacts/." -ForegroundColor Green
