param(
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$Notes,
    [string]$Checksum
)
$ErrorActionPreference = 'Stop'
python tools/verify_release.py --zip $Archive --manifest $Manifest --repository $env:RELEASE_REPO --tag $env:RELEASE_TAG
if ($LASTEXITCODE -ne 0) { throw 'Invalid package; release blocked' }
$metadata = gh release view $env:RELEASE_TAG --repo $env:RELEASE_REPO --json assets 2>$null
if ($LASTEXITCODE -eq 0) {
    $existing = $metadata | ConvertFrom-Json
    $name = Split-Path -Leaf $Archive
    if ($existing.assets.name -contains $name) {
        $download = Join-Path $env:RUNNER_TEMP ('existing-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $download | Out-Null
        gh release download $env:RELEASE_TAG --repo $env:RELEASE_REPO --pattern $name --dir $download
        if ($LASTEXITCODE -ne 0) { throw 'Existing package could not be verified' }
        $published = Join-Path $download $name
        python tools/verify_release.py --zip $published --manifest $Manifest --repository $env:RELEASE_REPO --tag $env:RELEASE_TAG
        if ($LASTEXITCODE -ne 0) { throw 'Existing release contradicts this version; refusing replacement' }
        if ($Checksum -and $existing.assets.name -notcontains (Split-Path -Leaf $Checksum)) {
            $sidecar = Join-Path $download (Split-Path -Leaf $Checksum)
            [IO.File]::WriteAllText($sidecar, (Get-FileHash -LiteralPath $published -Algorithm SHA256).Hash.ToLowerInvariant() + "  $name`n")
            gh release upload $env:RELEASE_TAG $sidecar --repo $env:RELEASE_REPO
            if ($LASTEXITCODE -ne 0) { throw 'Missing checksum upload failed' }
        }
        Write-Host 'Existing release package verified; no published assets replaced.'
        exit 0
    }
    gh release upload $env:RELEASE_TAG $Archive --repo $env:RELEASE_REPO
    if ($LASTEXITCODE -ne 0) { throw 'Package upload failed' }
    if ($Checksum) {
        gh release upload $env:RELEASE_TAG $Checksum --repo $env:RELEASE_REPO
        if ($LASTEXITCODE -ne 0) { throw 'Checksum upload failed' }
    }
} else {
    $assets = @($Archive)
    if ($Checksum) { $assets += $Checksum }
    gh release create $env:RELEASE_TAG @assets --verify-tag --repo $env:RELEASE_REPO --title $env:RELEASE_TAG --notes-file $Notes --latest=false
    if ($LASTEXITCODE -ne 0) { throw 'Release publication failed' }
}
