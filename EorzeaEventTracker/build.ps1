$ErrorActionPreference = 'Stop'
$localDotnet = Join-Path $PSScriptRoot '..\.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
& $dotnet build (Join-Path $PSScriptRoot 'EorzeaEventTracker\EorzeaEventTracker.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }
Write-Host "Build finished: $PSScriptRoot\EorzeaEventTracker\bin\Release\EorzeaEventTracker.dll" -ForegroundColor Green
