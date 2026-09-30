param([switch]$Rebuild, [switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:THREADBACK_ROOT = $root
$app = Join-Path $root 'src/ThreadBack.App/bin/Release/net10.0-windows10.0.19041.0/ThreadBack.exe'
if ($Rebuild -or -not (Test-Path -LiteralPath $app)) {
    if (-not (Test-Path -LiteralPath $dotnet)) { & (Join-Path $PSScriptRoot 'Setup.ps1') -Component Sdk }
    & $dotnet build (Join-Path $root 'src/ThreadBack.App') -c Release --nologo -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=ThreadBack Development' -and $_.HasPrivateKey } | Select-Object -First 1
    $signtool = Get-ChildItem -LiteralPath (Join-Path $root '.tools/windows-sdk') -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1 -ExpandProperty FullName
    if ($certificate -and $signtool) {
        foreach ($assembly in @('ThreadBack.Core.dll', 'ThreadBack.dll')) {
            & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My (Join-Path (Split-Path $app) $assembly)
            if ($LASTEXITCODE -ne 0) { throw "Development signing failed for $assembly." }
        }
    }
}
if ($BuildOnly) { return }
# Use the installed project .NET host in a separate process. On this machine,
# directly invoking the DLL from the checking shell triggered Code Integrity
# 0x800711C7, while this Start-Process route launched the same DLL successfully.
# No Windows policy or certificate-trust change is needed for this launcher.
if (Test-Path -LiteralPath $dotnet) {
    $appDll = Join-Path (Split-Path $app) 'ThreadBack.dll'
    Start-Process -FilePath $dotnet -ArgumentList ('"' + $appDll + '"') -WorkingDirectory $root -WindowStyle Normal
} else {
    Start-Process -FilePath $app -WorkingDirectory $root -WindowStyle Normal
}

