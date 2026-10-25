param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$candidates = @(
    (Join-Path $PSScriptRoot '..\..\.dotnet\dotnet.exe'),
    (Join-Path $PSScriptRoot '..\.dotnet\dotnet.exe')
)
$dotnet = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $dotnet) { $dotnet = 'dotnet' }
$buildArguments = @('build', (Join-Path $PSScriptRoot 'VendorProfit\VendorProfit.csproj'), '-c', 'Release', '--warnaserror', '-p:Use_DalamudPackager=false')
if ($Offline) { $buildArguments += '-p:NuGetAudit=false' }
& $dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "VendorProfit build failed ($LASTEXITCODE)" }
& $dotnet run --project (Join-Path $PSScriptRoot 'tests\Checks\Checks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'VendorProfit policy checks failed' }

# A small explicit package avoids packaging machine-specific dependencies supplied by Dalamud.
$output = Join-Path $PSScriptRoot 'VendorProfit\bin\Release'
$packageDirectory = Join-Path $PSScriptRoot 'artifacts'
[System.IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VendorProfit\VendorProfit.json') -Destination (Join-Path $output 'VendorProfit.json') -Force
$manifest = Get-Content -LiteralPath (Join-Path $output 'VendorProfit.json') -Raw | ConvertFrom-Json
$dll = Join-Path $output 'VendorProfit.dll'
$version = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($manifest.AssemblyVersion -ne $version -or $manifest.DalamudApiLevel -ne 15) { throw 'Manifest / assembly version or API mismatch' }
Add-Type -AssemblyName System.IO.Compression
$archive = Join-Path $packageDirectory 'VendorProfit.zip'
$stream = [System.IO.File]::Open($archive, [System.IO.FileMode]::Create)
try {
    $zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in @('VendorProfit.dll', 'VendorProfit.json')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $output $name), $name) | Out-Null
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $packageDirectory 'VendorProfit.zip.sha256'), "$hash  VendorProfit.zip`n")
Write-Host "Built and checked: $archive" -ForegroundColor Green

python (Join-Path $PSScriptRoot '..\tools\verify_release.py') --zip $archive --manifest (Join-Path $output 'VendorProfit.json') --repository karkandcoypp/FFXIV-Dalamud-Plugins --tag ('vp-v' + ($manifest.AssemblyVersion -replace '\.0$', ''))
if ($LASTEXITCODE -ne 0) { throw 'VendorProfit package validation failed' }
