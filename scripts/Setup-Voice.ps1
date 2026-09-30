$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $cache,(Join-Path $root 'models') | Out-Null
$arm = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64'
$asset = if ($arm) { 'whisper-bin-win-cpu-arm64.zip' } else { 'whisper-bin-x64.zip' }
$hash = if ($arm) { '799543b926ab5b6c2d60cab269a2092e0ae8d27820e9e15429e59de3699546fc' } else { 'f9ec6c52a2e949b62ab51fa21d0d497958f9e41c3010c157c4e42932d5316f3c' }
. (Join-Path $PSScriptRoot 'Download-Verified.ps1')
function Fetch([string]$Url, [string]$Path, [string]$Sha) {
    Get-VerifiedDownload -Url $Url -Destination $Path -Hash $Sha
}
Fetch "https://github.com/ggml-org/whisper.cpp/releases/download/b5130/$asset" (Join-Path $cache 'whisper.zip') $hash
Expand-Archive -LiteralPath (Join-Path $cache 'whisper.zip') -DestinationPath (Join-Path $cache 'whisper') -Force
Fetch 'https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.en.bin' (Join-Path $root 'models/ggml-base.en.bin') 'a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002'
Write-Output 'Whisper Base English ready. CPU transcription; Snapdragon NPU adapter remains a separate validation step.'
