<#
.SYNOPSIS
    Erstellt die Dateien für ein GitHub-Release: veröffentlichte EXE und SHA-256-Prüfsumme.
    Ergebnis: artifacts/release/OrbitStudio.exe und OrbitStudio.exe.sha256 (Namen wie vom Updater erwartet).
    Das Hochladen übernimmt danach „gh release create“ (siehe docs/build-and-release.de.md).
#>
. (Join-Path $PSScriptRoot 'common.ps1')

$propsPath = Join-Path $RepoRoot 'Directory.Build.props'
$version = [regex]::Match((Get-Content $propsPath -Raw), '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { throw 'Keine Version in Directory.Build.props gefunden.' }

& (Join-Path $PSScriptRoot 'publish.ps1')

$release = Join-Path $Artifacts 'release'
New-Item -ItemType Directory -Force $release | Out-Null
$exe = Join-Path $release 'OrbitStudio.exe'
Copy-Item (Join-Path $Artifacts 'publish/OrbitStudio.exe') $exe -Force

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  OrbitStudio.exe" | Set-Content -Path (Join-Path $release 'OrbitStudio.exe.sha256') -Encoding ascii

Write-Host "Version $version bereit:" -ForegroundColor Green
Write-Host "  $exe"
Write-Host "  SHA-256 $hash"
