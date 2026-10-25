param(
    [ValidateSet('All', 'UltrawideCutscenes', 'EorzeaEventTracker', 'EorzeaCompass', 'BagSense', 'VendorProfit')]
    [string]$Plugin = 'All'
)
$ErrorActionPreference = 'Stop'
$candidates = @(
    (Join-Path $PSScriptRoot '.dotnet\dotnet.exe'),
    (Join-Path $PSScriptRoot '..\.dotnet\dotnet.exe')
)
$dotnet = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $dotnet) { $dotnet = 'dotnet' }
$projects = [ordered]@{
    UltrawideCutscenes = 'UltrawideCutscenes\Dalamud.FullscreenCutscenes.sln'
    EorzeaEventTracker = 'EorzeaEventTracker\EorzeaEventTracker\EorzeaEventTracker.csproj'
    EorzeaCompass = 'EorzeaCompass\EorzeaCompass\EorzeaCompass.csproj'
    BagSense = 'BagSense\BagSense\BagSense.csproj'
    VendorProfit = 'VendorProfit\VendorProfit\VendorProfit.csproj'
}
foreach ($name in $projects.Keys) {
    if ($Plugin -ne 'All' -and $Plugin -ne $name) { continue }
    if ($name -in @('BagSense', 'VendorProfit')) {
        & (Join-Path $PSScriptRoot "$name\build.ps1")
        continue
    }
    & $dotnet build (Join-Path $PSScriptRoot $projects[$name]) -c Release --warnaserror
    if ($LASTEXITCODE -ne 0) { throw "$name build failed (exit $LASTEXITCODE)" }
}
