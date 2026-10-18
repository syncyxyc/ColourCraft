<#
.SYNOPSIS
    Führt alle Testprojekte und den UI-Smoke-Test aus.
    Ergebnisse: artifacts/testresults/*.trx, smoke-test.txt, smoke-test.png
.PARAMETER SkipSmoke
    Überspringt den UI-Smoke-Test (z. B. ohne interaktive Sitzung).
#>
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipSmoke
)

. (Join-Path $PSScriptRoot 'common.ps1')

$results = Join-Path $Artifacts 'testresults'
New-Item -ItemType Directory -Force $results | Out-Null

Write-Host "== Unit- und Architekturtests ($Configuration) ==" -ForegroundColor Cyan
Invoke-Dotnet test $Solution -c $Configuration --logger 'trx' --results-directory $results

if ($SkipSmoke) {
    Write-Host 'UI-Smoke-Test übersprungen (-SkipSmoke).' -ForegroundColor Yellow
    return
}

Write-Host '== UI-Smoke-Test ==' -ForegroundColor Cyan
$exe = Join-Path $RepoRoot "src/OrbitStudio.App/bin/$Configuration/net10.0-windows/OrbitStudio.exe"
if (-not (Test-Path $exe)) { throw "Programm nicht gefunden: $exe (zuerst build.ps1 ausführen)." }

$resultFile = Join-Path $results 'smoke-test.txt'
Remove-Item $resultFile -ErrorAction SilentlyContinue
$process = Start-Process $exe -ArgumentList "--smoke-test `"$resultFile`"" -PassThru
if (-not $process.WaitForExit(120000)) {
    $process.Kill()
    throw 'UI-Smoke-Test: Zeitüberschreitung nach 120 s.'
}
Get-Content $resultFile -Encoding UTF8 | ForEach-Object { Write-Host $_ }
if ($process.ExitCode -ne 0) { throw "UI-Smoke-Test fehlgeschlagen (Exitcode $($process.ExitCode))." }
Write-Host 'Alle Tests bestanden.' -ForegroundColor Green
