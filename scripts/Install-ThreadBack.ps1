param([switch]$SkipVision, [switch]$EnableGpu, [switch]$NoLaunch, [switch]$NoShortcut)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path $PSScriptRoot -Parent
$logDirectory = Join-Path $root 'artifacts/verification'
$transcribing = $false
$locationChanged = $false
try {
    if ([Environment]::OSVersion.Platform -ne 'Win32NT' -or [Environment]::OSVersion.Version.Build -lt 19041) {
        throw 'ThreadBack requires Windows 10 version 2004 or later, or Windows 11.'
    }
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    if ($architecture -notin @('X64','Arm64')) { throw 'Use a 64-bit Windows PC (Intel/AMD x64 or ARM64).' }
    if ($EnableGpu -and $architecture -ne 'X64') { throw 'Optional GPU setup currently supports Windows x64 only. Run without -EnableGpu on ARM64.' }
    if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) { throw 'Windows curl.exe is required for model downloads.' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $log = Join-Path $logDirectory ("install-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    Start-Transcript -LiteralPath $log | Out-Null
    $transcribing = $true
    Push-Location -LiteralPath $root
    $locationChanged = $true
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:THREADBACK_ROOT = $root
    Write-Host "Setting up ThreadBack for $architecture. No API key is needed."
    & (Join-Path $PSScriptRoot 'Setup-NativeRuntime.ps1')
    Write-Host 'Text, voice, and image AI plus build tools need roughly 6 GB of downloads; allow at least 12 GB free space.'
    Write-Host '[1/5] Installing the local .NET SDK and text AI...'
    & (Join-Path $PSScriptRoot 'Setup.ps1')
    Write-Host '[2/5] Installing voice transcription...'
    & (Join-Path $PSScriptRoot 'Setup-Voice.ps1')
    Write-Host '[3/5] Installing image understanding...'
    if (-not $SkipVision) { & (Join-Path $PSScriptRoot 'Setup.ps1') -Component Vision }
    else { Write-Host 'Image model skipped. Windows OCR remains available.' }
    if ($EnableGpu) { & (Join-Path $PSScriptRoot 'Setup.ps1') -Component Gpu }
    Write-Host '[4/5] Checking local dependencies and building ThreadBack...'
    $dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
    $runtimes = & $dotnet --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.WindowsDesktop.App 10\.')) { throw 'The local .NET 10 Desktop Runtime is missing.' }
    $required = @('.tools/llama/llama-server.exe','models/Qwen3-4B-Q4_K_M.gguf','models/ggml-base.en.bin')
    if (-not $SkipVision) { $required += @('models/vision/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf','models/vision/mmproj-Qwen2.5-VL-3B-Instruct-Q8_0.gguf') }
    foreach ($relative in $required) { if (-not (Test-Path -LiteralPath (Join-Path $root $relative))) { throw "Missing dependency: $relative" } }
    $voice = Get-ChildItem -LiteralPath (Join-Path $root '.tools/whisper') -Filter whisper-cli.exe -Recurse | Select-Object -First 1
    if (-not $voice) { throw 'The voice transcription executable is missing.' }
    & (Join-Path $root '.tools/llama/llama-server.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'The text/image AI runtime cannot start. Check the setup log.' }
    & $voice.FullName --help
    if ($LASTEXITCODE -ne 0) { throw 'The voice AI runtime cannot start. Check the setup log.' }
    & (Join-Path $PSScriptRoot 'Run.ps1') -Rebuild -BuildOnly
    Write-Host '[5/5] Connecting the launcher to this installation...'
    if (-not $NoShortcut) {
        $desktop = [Environment]::GetFolderPath('Desktop')
        if ($desktop -and (Test-Path -LiteralPath $desktop)) {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut((Join-Path $desktop 'ThreadBack.lnk'))
            $shortcut.TargetPath = Join-Path $root 'ThreadBack.cmd'
            $shortcut.WorkingDirectory = $root
            $shortcut.IconLocation = (Join-Path $root 'src/ThreadBack.App/Assets/ThreadBack.ico') + ',0'
            $shortcut.Save()
        }
    }
    Write-Host 'Setup complete. Keep this project folder: the app loads its local AI from .tools and models.'
    Write-Host 'Open ThreadBack.cmd or the desktop shortcut. AI models load when you use their features.'
    if ($EnableGpu) { Write-Host 'Choose GPU in Model settings if your hardware is detected. CPU remains the default.' }
    Write-Host "Setup log: $log"
    if (-not $NoLaunch) { & (Join-Path $PSScriptRoot 'Run.ps1') }
} catch {
    Write-Host "Setup stopped: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Keep the log and partial downloads. Setup did not report success.'
    exit 1
} finally {
    if ($locationChanged) { Pop-Location }
    if ($transcribing) { Stop-Transcript | Out-Null }
}
