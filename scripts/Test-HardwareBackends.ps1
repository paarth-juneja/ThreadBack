param([ValidateSet('Gpu','Npu')][string]$Device)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
$checks = Join-Path $root 'tests/ThreadBack.Checks'
$report = Join-Path $root "artifacts/verification/$($Device.ToLowerInvariant())-backend-user-check.txt"
if ($Device -eq 'Npu' -and -not (Test-Path (Join-Path $root '.tools/llama-openvino/llama-server.exe'))) {
    throw 'The verified OpenVINO download is incomplete. NPU testing has not started.'
}
$flags = if ($Device -eq 'Gpu') { @('--gpu-model','--gpu-vision') } else { @('--npu-model') }
& $dotnet run --project $checks -c Release -- $flags *> $report
Get-Content $report -Tail 20
if ($LASTEXITCODE -ne 0) { throw "$Device backend check failed. See $report. The NPU switch remains disabled until its model check passes." }
if ($Device -eq 'Npu' -and ((-not (Select-String -Path $report -Pattern 'NPU MODEL PASS:' -Quiet)) -or (-not (Test-Path (Join-Path $root 'artifacts/verification/npu-model-verified.json'))))) {
    throw "NPU check did not pass. See $report. The NPU switch remains disabled."
}
