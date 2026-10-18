<#
.SYNOPSIS
    Stellt Pakete wieder her und baut die Solution (Debug und Release).
.PARAMETER Configuration
    Debug, Release oder All (Standard).
#>
param([ValidateSet('Debug', 'Release', 'All')][string]$Configuration = 'All')

. (Join-Path $PSScriptRoot 'common.ps1')

Invoke-Dotnet restore $Solution
$configs = if ($Configuration -eq 'All') { @('Debug', 'Release') } else { @($Configuration) }
foreach ($config in $configs) {
    Write-Host "== Build $config ==" -ForegroundColor Cyan
    Invoke-Dotnet build $Solution -c $config --no-restore
}
Write-Host 'Build erfolgreich.' -ForegroundColor Green
