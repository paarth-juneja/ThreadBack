# Shared downloader: reuse verified files and resume interrupted transfers.
function Get-VerifiedDownload {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][string]$Hash,
        [ValidateSet('SHA256','SHA512')][string]$Algorithm = 'SHA256'
    )
    if ((Test-Path -LiteralPath $Destination) -and (Get-FileHash -LiteralPath $Destination -Algorithm $Algorithm).Hash -eq $Hash) { return }
    $partial = "$Destination.partial"
    if ((Test-Path -LiteralPath $partial) -and (Get-FileHash -LiteralPath $partial -Algorithm $Algorithm).Hash -eq $Hash) {
        Move-Item -LiteralPath $partial -Destination $Destination -Force
        return
    }
    Write-Host "Downloading $(Split-Path $Destination -Leaf). Large models may take a while."
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        & curl.exe -4 --http1.1 --show-error --fail --location --connect-timeout 30 --max-time 0 --speed-limit 1024 --speed-time 60 --continue-at - --output $partial $Url
        if ($LASTEXITCODE -eq 0) { break }
        Write-Host "Transfer interrupted (attempt $attempt of 4)."
    }
    if ($LASTEXITCODE -ne 0) { throw "Download incomplete: $Destination. Partial data was kept for a later resume." }
    if ((Get-FileHash -LiteralPath $partial -Algorithm $Algorithm).Hash -ne $Hash) { throw "Checksum mismatch: $partial. Do not use this file; inspect it before retrying." }
    Move-Item -LiteralPath $partial -Destination $Destination -Force
}
