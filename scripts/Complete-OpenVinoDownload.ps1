$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$archive = Join-Path $root '.tools/llama-openvino.zip'
$destination = Join-Path $root '.tools/llama-openvino'
$report = Join-Path $root 'artifacts/verification/openvino-download-state.json'
$url = 'https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-openvino-2026.3.1-x64.zip'
$expectedHash = 'f607ea279c0ffd85c13bcd050768631c73b400d16c6e5e1de9ff468ad33c6456'
function Write-State([string]$status, [string]$detail) {
    [pscustomobject]@{
        status = $status
        detail = $detail
        bytes = if (Test-Path -LiteralPath $archive) { (Get-Item -LiteralPath $archive).Length } else { 0 }
        updatedAt = (Get-Date).ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath $report
}
Write-State 'downloading' 'Final authorized resume attempt (attempt 4); no further retry if this fails.'
try {
    & curl.exe -L --fail --silent --show-error --connect-timeout 20 --max-time 1800 --continue-at - -o $archive $url
    if ($LASTEXITCODE -ne 0) { throw "curl failed with exit code $LASTEXITCODE" }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expectedHash) { throw "SHA256 mismatch: $hash" }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
    if (-not (Test-Path -LiteralPath (Join-Path $destination 'llama-server.exe'))) { throw 'Verified archive did not contain llama-server.exe at the expected path.' }
    Write-State 'completed' "SHA256 verified: $hash. OpenVINO runtime extracted. NPU model validation is still pending."
} catch {
    Write-State 'failed' $_.Exception.Message
    exit 1
}
