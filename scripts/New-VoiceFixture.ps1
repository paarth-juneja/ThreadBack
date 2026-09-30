# Synthetic test audio only; does not open or record the microphone.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$voice = New-Object -ComObject SAPI.SpVoice
$stream = New-Object -ComObject SAPI.SpFileStream
$stream.Open((Join-Path $folder 'voice-fixture.wav'), 3)
$voice.AudioOutputStream = $stream
$voice.Rate = -1
$voice.Speak('I rejected approach A because it requires the internet. When I return, measure the memory usage of approach B. I have not selected a final approach.') | Out-Null
$stream.Close()
Write-Output 'Synthetic voice fixture ready.'
