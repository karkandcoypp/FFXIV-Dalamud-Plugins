$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '..\build.ps1') -Plugin EorzeaEventTracker
Write-Host "Build finished: $PSScriptRoot\EorzeaEventTracker\bin\Release\EorzeaEventTracker.dll" -ForegroundColor Green
