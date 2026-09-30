param([switch]$Model, [switch]$Evaluation)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$arguments = @('run','--project',(Join-Path $root 'tests/ThreadBack.Checks'),'-c','Release','--')
if ($Model) { $arguments += @('--model','--root',$root) }
if ($Evaluation) { $arguments += @('--evaluation','--root',$root) }
& (Join-Path $root '.tools/dotnet/dotnet.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
