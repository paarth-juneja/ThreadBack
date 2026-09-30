# Development recovery helper; requires PowerShell 7 for parallel range requests.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$partial = Join-Path $root 'models/Qwen3-4B-Q4_K_M.gguf.partial'
$target = Join-Path $root 'models/Qwen3-4B-Q4_K_M.gguf'
$expected = [long]2497280256
$sha = '7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5'
if (Test-Path -LiteralPath $target) { if ((Get-FileHash -LiteralPath $target).Hash -eq $sha) { Write-Output 'Model already verified.'; exit }; throw 'Existing model checksum mismatch.' }
$offset = (Get-Item -LiteralPath $partial).Length
if ($offset -gt $expected) { throw 'Partial file is larger than the expected model.' }
$chunkDir = Join-Path $root '.tools/model-download-chunks'
New-Item -ItemType Directory -Force -Path $chunkDir | Out-Null
$chunks = @()
for ($position = $offset; $position -lt $expected; $position += 64MB) {
    $last = [Math]::Min($expected - 1, $position + 64MB - 1)
    $chunks += [pscustomobject]@{ Start=$position; End=$last; Length=$last-$position+1; Path=(Join-Path $chunkDir "$position.bin") }
}
$chunks | ForEach-Object -Parallel {
    $ErrorActionPreference = 'Continue'
    $PSNativeCommandUseErrorActionPreference = $false
    $chunk = $_
    if ((Test-Path -LiteralPath $chunk.Path) -and (Get-Item -LiteralPath $chunk.Path).Length -eq $chunk.Length) { return }
    $url = 'https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/bc640142c66e1fdd12af0bd68f40445458f3869b/Qwen3-4B-Q4_K_M.gguf?download=true'
    for ($attempt=1; $attempt -le 4; $attempt++) {
        & curl.exe -4 --silent --show-error --fail --location --connect-timeout 15 --max-time 600 --speed-time 60 --speed-limit 1024 --range "$($chunk.Start)-$($chunk.End)" --max-filesize $chunk.Length --output $chunk.Path $url
        if ($LASTEXITCODE -eq 0 -and (Get-Item -LiteralPath $chunk.Path).Length -eq $chunk.Length) { Write-Output "Downloaded range $($chunk.Start)-$($chunk.End)"; return }
    }
    throw "Range download failed at $($chunk.Start). Run again to reuse completed ranges."
} -ThrottleLimit 4
foreach ($chunk in $chunks) { if (-not (Test-Path -LiteralPath $chunk.Path) -or (Get-Item -LiteralPath $chunk.Path).Length -ne $chunk.Length) { throw 'A model range is incomplete.' } }
$stream = [IO.File]::Open($partial,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {
    if ($stream.Length -ne $offset) { throw 'Another process changed the partial download.' }
    $stream.Position = $offset
    foreach ($chunk in $chunks) { $inputStream = [IO.File]::OpenRead($chunk.Path); try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() } }
    $stream.Flush($true)
} finally { $stream.Dispose() }
if ((Get-FileHash -LiteralPath $partial).Hash -ne $sha) { throw 'Model checksum mismatch; not activated.' }
Move-Item -LiteralPath $partial -Destination $target
Write-Output 'Qwen3 4B downloaded and verified.'
