param(
    [ValidateSet('submit','status','frameworks','retry-debug','submit-repaired-encoder','submit-decoder-binary')][string]$Action = 'status',
    [string]$ModelPath,
    [ValidateSet('encoder','decoder')][string]$Component
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$secretPath = Join-Path $env:LOCALAPPDATA 'ThreadBack/aihub-token.dpapi'
if (-not (Test-Path -LiteralPath $secretPath)) { throw 'Connect AI Hub first using Connect-AIHub.cmd.' }
if ($Action -eq 'submit' -and ([string]::IsNullOrWhiteSpace($ModelPath) -or [string]::IsNullOrWhiteSpace($Component))) {
    throw 'Submit requires -ModelPath and -Component.'
}
if ($Action -eq 'retry-debug' -and [string]::IsNullOrWhiteSpace($Component)) {
    throw 'retry-debug requires -Component.'
}
$secret = (Get-Content -LiteralPath $secretPath -Raw).Trim() | ConvertTo-SecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $arguments = @((Join-Path $PSScriptRoot 'aihub_profile.py'), $Action)
    if ($Action -eq 'submit') { $arguments += @('--model', $ModelPath, '--component', $Component) }
    if ($Action -eq 'retry-debug') { $arguments += @('--component', $Component) }
    $plain | & (Join-Path $root '.tools/aihub-python/Scripts/python.exe') @arguments
    if ($LASTEXITCODE -ne 0) { throw 'AI Hub operation did not finish. Inspect the non-secret job record before retrying submission.' }
} finally {
    $plain = $null
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $secret.Dispose()
}
