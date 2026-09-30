param([ValidateSet('x64','arm64')][string]$Architecture = 'x64', [switch]$Install, [switch]$SelfContained)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
$sdkVersion = '10.0.26100.9169'
$sdkPath = Join-Path $root '.tools/windows-sdk'
if (-not (Test-Path -LiteralPath $sdkPath)) {
    $zip = Join-Path $root '.tools/windows-sdk.zip'
    & curl.exe --silent --show-error --fail --location --retry 6 --retry-all-errors --output $zip "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$sdkVersion/microsoft.windows.sdk.buildtools.$sdkVersion.nupkg"
    if ($LASTEXITCODE -ne 0) { throw 'Windows packaging tools download failed.' }
    Expand-Archive -LiteralPath $zip -DestinationPath $sdkPath
}
$hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
$makeappx = Get-ChildItem -LiteralPath $sdkPath -Filter makeappx.exe -Recurse | Where-Object { $_.Directory.Name -eq $hostArch } | Select-Object -First 1 -ExpandProperty FullName
$signtool = Get-ChildItem -LiteralPath $sdkPath -Filter signtool.exe -Recurse | Where-Object { $_.Directory.Name -eq $hostArch } | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeappx -or -not $signtool) { throw 'Windows packaging tools not found.' }
$stage = Join-Path $root "artifacts/package-$Architecture"
& $dotnet publish (Join-Path $root 'src/ThreadBack.App') -c Release -r "win-$Architecture" --self-contained $SelfContained.ToString().ToLowerInvariant() -o $stage --nologo -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$assets = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Add-Type -AssemblyName System.Drawing
$logoImage = [System.Drawing.Image]::FromFile((Join-Path $root 'src/ThreadBack.App/Assets/ThreadBack.png'))
foreach ($size in @(44,50,150)) {
    $bitmap = [System.Drawing.Bitmap]::new($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($logoImage,0,0,$size,$size)
    $bitmap.Save((Join-Path $assets "Logo$size.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}
$logoImage.Dispose()
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
  <Identity Name="ThreadBack.Prototype" Publisher="CN=ThreadBack Development" Version="0.1.0.0" ProcessorArchitecture="$Architecture" />
  <Properties><DisplayName>ThreadBack</DisplayName><PublisherDisplayName>ThreadBack Development</PublisherDisplayName><Logo>Assets\Logo50.png</Logo></Properties>
  <Resources><Resource Language="en-us"/></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0"/></Dependencies>
  <Applications><Application Id="App" Executable="ThreadBack.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements DisplayName="ThreadBack" Description="A local handoff to your future self" BackgroundColor="#7151BF" Square150x150Logo="Assets\Logo150.png" Square44x44Logo="Assets\Logo44.png"/></Application></Applications>
  <Capabilities><rescap:Capability Name="runFullTrust"/><DeviceCapability Name="microphone"/></Capabilities>
</Package>
"@
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest)
$package = Join-Path $root "artifacts/ThreadBack-$Architecture.msix"
& $makeappx pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }
$certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=ThreadBack Development' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(7) } | Select-Object -First 1
if (-not $certificate) { $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=ThreadBack Development' -FriendlyName 'ThreadBack local prototype signing' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddMonths(3) }
& $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My $package
if ($LASTEXITCODE -ne 0) { throw 'Package signing failed.' }
$cer = Join-Path $root 'artifacts/ThreadBack-Development.cer'
Export-Certificate -Cert $certificate -FilePath $cer -Force | Out-Null
if ($Install) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $administrator = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $administrator) { throw 'The package was built and signed. Installing a development MSIX requires machine certificate trust. Use the direct launcher, or run this script with -Install from an administrator PowerShell for your own account.' }
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    Add-AppxPackage -Path $package -ForceApplicationShutdown
    $config = Join-Path $env:LOCALAPPDATA 'ThreadBack'
    New-Item -ItemType Directory -Force -Path $config | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $config 'runtime-root.txt'), $root)
    Write-Output 'ThreadBack installed for this Windows account. Open it from Start.'
}
Write-Output "Package: $package"
