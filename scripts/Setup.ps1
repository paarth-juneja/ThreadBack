param([ValidateSet('All','Sdk','Model','Vision','Gpu','Npu')][string]$Component = 'All')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
. (Join-Path $PSScriptRoot 'Download-Verified.ps1')
function Fetch-Verified([string]$Url, [string]$Destination, [string]$Hash, [string]$Algorithm = 'SHA256') {
    Get-VerifiedDownload -Url $Url -Destination $Destination -Hash $Hash -Algorithm $Algorithm
}
if ($Component -in @('All','Sdk')) {
    if (-not (Test-Path -LiteralPath (Join-Path $cache 'dotnet/sdk/10.0.401'))) {
        $architecture = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
        $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
        $sdk = $metadata.releases.sdk | Where-Object { $_.version -eq '10.0.401' } | Select-Object -First 1
        $asset = $sdk.files | Where-Object { $_.rid -eq "win-$architecture" -and $_.url.EndsWith('.zip') } | Select-Object -First 1
        if (-not $asset) { throw 'Pinned .NET SDK asset not found.' }
        Fetch-Verified $asset.url (Join-Path $cache 'sdk.zip') $asset.hash 'SHA512'
        Expand-Archive -LiteralPath (Join-Path $cache 'sdk.zip') -DestinationPath (Join-Path $cache 'dotnet') -Force
    }
    Write-Output 'SDK ready.'
}
if ($Component -in @('All','Model')) {
    $architecture = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
    $runtimeHash = if ($architecture -eq 'arm64') { '4b6a004b076eea47c318bea35cf1db2ff2bf037738b04645646ae8d7c3159478' } else { '917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7' }
    Fetch-Verified "https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-cpu-$architecture.zip" (Join-Path $cache 'llama.zip') $runtimeHash
    Expand-Archive -LiteralPath (Join-Path $cache 'llama.zip') -DestinationPath (Join-Path $cache 'llama') -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'models') | Out-Null
    Fetch-Verified 'https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/bc640142c66e1fdd12af0bd68f40445458f3869b/Qwen3-4B-Q4_K_M.gguf?download=true' (Join-Path $root 'models/Qwen3-4B-Q4_K_M.gguf') '7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5'
    Write-Output 'Local AI ready: llama.cpp b10964 / Qwen3 4B Q4_K_M / CPU.'
}
if ($Component -eq 'Vision') {
    $visionDir = Join-Path $root 'models/vision'
    New-Item -ItemType Directory -Force -Path $visionDir | Out-Null
    $revision = '5037fcf163dd95d1e41d1974465f0898ed108ca2'
    $base = "https://huggingface.co/ggml-org/Qwen2.5-VL-3B-Instruct-GGUF/resolve/$revision"
    Fetch-Verified "$base/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf" (Join-Path $visionDir 'Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf') 'd02fe9b69ad8cadbbd228e387667af66612c44bed29ffc8eb1e7caf9ac486c12'
    Fetch-Verified "$base/mmproj-Qwen2.5-VL-3B-Instruct-Q8_0.gguf" (Join-Path $visionDir 'mmproj-Qwen2.5-VL-3B-Instruct-Q8_0.gguf') '980c9b2f78c04e6cff93d277ada09e768394f112d75db3b4e9dea8a69f9fb904'
    Write-Output 'Local image understanding ready: Qwen2.5-VL 3B Q4_K_M / CPU.'
}
if ($Component -eq 'Gpu') {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'The pinned Vulkan runtime is available here only for Windows x64.' }
    Fetch-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-vulkan-x64.zip' (Join-Path $cache 'llama-vulkan.zip') '1ee3ad952f4ba71f438bd6d7bebef19e1c7af04adcaa35d08b4ddabb27d4c642'
    Expand-Archive -LiteralPath (Join-Path $cache 'llama-vulkan.zip') -DestinationPath (Join-Path $cache 'llama-vulkan') -Force
    Write-Output 'Vulkan model backend installed. Model settings checks for a usable GPU before enabling it.'
}
if ($Component -eq 'Npu') {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'The pinned OpenVINO runtime is available here only for Windows x64.' }
    Fetch-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b10964/llama-b10964-bin-win-openvino-2026.3.1-x64.zip' (Join-Path $cache 'llama-openvino.zip') 'f607ea279c0ffd85c13bcd050768631c73b400d16c6e5e1de9ff468ad33c6456'
    Expand-Archive -LiteralPath (Join-Path $cache 'llama-openvino.zip') -DestinationPath (Join-Path $cache 'llama-openvino') -Force
    Write-Output 'OpenVINO backend installed. Model settings checks for a usable NPU before enabling it.'
}
