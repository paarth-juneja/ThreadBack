param([switch]$UseSaved)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$python = Join-Path $root '.tools/aihub-python/Scripts/python.exe'
$secretDirectory = Join-Path $env:LOCALAPPDATA 'ThreadBack'
$secretPath = Join-Path $secretDirectory 'aihub-token.dpapi'
if (-not (Test-Path -LiteralPath $python)) { throw 'The local AI Hub client has not been installed yet.' }
if ($UseSaved) {
    if (-not (Test-Path -LiteralPath $secretPath)) { throw 'Run Connect-AIHub.cmd once to enter your key privately.' }
    $secret = (Get-Content -LiteralPath $secretPath -Raw).Trim() | ConvertTo-SecureString
} else {
    Write-Host 'Paste your Qualcomm AI Hub API key below. It will not be displayed.'
    Write-Host 'After a successful connection, it is protected for your Windows account outside the project.'
    $secret = Read-Host 'API key' -AsSecureString
}
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($plain)) { throw 'No API key entered.' }
    $plain | & $python (Join-Path $PSScriptRoot 'aihub_devices.py')
    if ($LASTEXITCODE -ne 0) { throw 'Connection check failed. The new key was not saved. Check your key and internet connection, then retry.' }
    if (-not $UseSaved) {
        New-Item -ItemType Directory -Force -Path $secretDirectory | Out-Null
        $secret | ConvertFrom-SecureString | Set-Content -LiteralPath $secretPath
    }
    Write-Host 'Connected. Device information is saved; tell Codex: connected.' -ForegroundColor Green
} finally {
    $plain = $null
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $secret.Dispose()
}
