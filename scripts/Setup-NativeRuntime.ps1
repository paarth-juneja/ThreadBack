$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($architecture -notin @('x64','arm64')) { throw 'The AI workers require Windows x64 or ARM64.' }
$systemDirectory = [Environment]::SystemDirectory
$missing = @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll') | Where-Object { -not (Test-Path -LiteralPath (Join-Path $systemDirectory $_)) }
if (-not $missing) { Write-Host 'Microsoft Visual C++ runtime is already available.'; return }

# Official architecture-specific installer; verify Microsoft's signature before executing.
# Reference: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist
$cache = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$installer = Join-Path $cache "vc_redist.$architecture.exe"
Write-Host 'Installing the Microsoft Visual C++ runtime required by the AI workers.'
Write-Host 'Windows may ask for administrator approval for this system dependency.'
& curl.exe -4 --http1.1 --show-error --fail --location --connect-timeout 30 --max-time 300 --output "$installer.partial" "https://aka.ms/vc14/vc_redist.$architecture.exe"
if ($LASTEXITCODE -ne 0) { throw 'Microsoft Visual C++ runtime download failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath "$installer.partial"
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') {
    throw 'The Visual C++ installer does not have a valid Microsoft signature. It was not executed.'
}
Move-Item -LiteralPath "$installer.partial" -Destination $installer -Force
$process = Start-Process -FilePath $installer -ArgumentList '/install','/quiet','/norestart' -Verb RunAs -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -in @(3010,1641)) { throw 'The Visual C++ runtime requires a Windows restart. Restart, then rerun ThreadBack setup.' }
if ($process.ExitCode -ne 0) { throw "Visual C++ runtime installation failed (exit $($process.ExitCode))." }
foreach ($dll in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $systemDirectory $dll))) { throw "Visual C++ installation finished but $dll is still missing." }
}
Write-Host 'Microsoft Visual C++ runtime is ready.'
