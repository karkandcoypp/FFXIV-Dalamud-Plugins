param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$candidates = @(
    (Join-Path $PSScriptRoot '..\..\.dotnet\dotnet.exe'),
    (Join-Path $PSScriptRoot '..\.dotnet\dotnet.exe')
)
$dotnet = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $dotnet) { $dotnet = 'dotnet' }
$buildArguments = @('build', (Join-Path $PSScriptRoot 'BagSense\BagSense.csproj'), '-c', 'Release', '--warnaserror', '-p:Use_DalamudPackager=false')
if ($Offline) { $buildArguments += '-p:NuGetAudit=false' }
& $dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "BagSense build failed ($LASTEXITCODE)" }
& $dotnet run --project (Join-Path $PSScriptRoot 'tests\PolicyChecks\PolicyChecks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "BagSense policy checks failed ($LASTEXITCODE)" }
python -m unittest discover -s (Join-Path $PSScriptRoot 'tests') -p test_database.py -v
if ($LASTEXITCODE -ne 0) { throw 'BagSense database collector checks failed' }
& $dotnet run --project (Join-Path $PSScriptRoot 'tools\ApiProbe\ApiProbe.csproj') -c Release -- --database (Join-Path $PSScriptRoot 'data')
if ($LASTEXITCODE -ne 0) { throw 'BagSense catalog integrity checks failed' }

# A small explicit package avoids packaging machine-specific dependencies supplied by Dalamud.
$output = Join-Path $PSScriptRoot 'BagSense\bin\Release'
$packageDirectory = Join-Path $PSScriptRoot 'artifacts'
[System.IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BagSense\BagSense.json') -Destination (Join-Path $output 'BagSense.json') -Force
$manifest = Get-Content -LiteralPath (Join-Path $output 'BagSense.json') -Raw | ConvertFrom-Json
$dll = Join-Path $output 'BagSense.dll'
$version = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
if ($manifest.AssemblyVersion -ne $version -or $manifest.DalamudApiLevel -ne 15) { throw 'Manifest / assembly version or API mismatch' }
Add-Type -AssemblyName System.IO.Compression
$archive = Join-Path $packageDirectory 'BagSense.zip'
$stream = [System.IO.File]::Open($archive, [System.IO.FileMode]::Create)
try {
    $zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in @('BagSense.dll', 'BagSense.json')) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $output $name), $name) | Out-Null
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $packageDirectory 'BagSense.zip.sha256'), "$hash  BagSense.zip`n")
Write-Host "Built and checked: $archive" -ForegroundColor Green
