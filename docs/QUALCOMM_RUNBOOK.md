# Qualcomm validation: first handoff

## Your next action

Sign in to https://qdc.qualcomm.com and look for a **Windows Snapdragon compute device**. Tell the assistant its listed model and your available session minutes. A phone or Dragonwing Linux board is not a substitute for testing this Windows application.

Do not reserve a long session just to wait for code. A compact ARM64 CPU transfer bundle is prepared at `artifacts/ThreadBack-cloud-arm64.zip` (1,184,093,748 bytes; SHA-256 `9F270C7105CD2C289889B174A17D11EA569A430C0140B78D64424B71F6ADE10D`). It includes a self-contained app, ARM64 CPU runtime, and a 1.5B text model. It does not include the image or speech models. Confirm the selected device is Windows ARM64 before uploading.

For the first session, use the QDC compute-device file browser to upload the ZIP, extract it on the remote device, run the included `Inspect-QualcommDevice.ps1`, then `Launch-ThreadBack.ps1`. The included `CLOUD-TEST.txt` has the short app sequence. Record the device model and pass/fail outcome, and download the session recording and any logs before ending. QDC also collects files under `C:\Temp\QDC_Logs` as session artifacts. A successful run establishes the CPU app workflow on that named Snapdragon device; it does not establish NPU or HP-specific performance.

## Inside the reserved Windows device

Open PowerShell. Copy the contents of `scripts/Inspect-QualcommDevice.ps1` into it and run them. Return the JSON output. The script is read-only and reports the device model, processor, Windows build, available runtimes, and relevant device entries. It does not print tokens or passwords.

## What the assistant will do with that output

1. Match x64/ARM64 application and native worker binaries to the device.
2. Check the required .NET Desktop Runtime and speech-inference prerequisites.
3. Prepare the CPU reference test and the compatible Qualcomm speech example.
4. Give you exact commands and expected output for any steps that require your remote session.
5. Interpret the returned results and update the proposal, deck, and demo script.

## AI Hub account

Sign in separately to https://aihub.qualcomm.com. Confirm that you can reach Workbench and see its device list or job interface. Keep the API token in the environment where the profiling script runs. Never paste it into chat or commit it into the project.

The NPU workflow will follow the official Windows Whisper example:
https://aihub.qualcomm.com/apps/whisper_windows_py

The exact commands must be pinned to the example revision and the available device/runtime. They are intentionally not presented as already verified in this prototype.

## Measurements we need

- Exact device model, chipset, Windows build, and runtime/model revisions.
- CPU and NPU transcription on the same synthetic audio when supported.
- Cold startup separately from warm transcription/inference.
- Full application save, generation, source inspection, restart, and reopen.
- Evidence of which execution provider actually ran the model.

Reference-device performance is reported as reference-device performance. Only an actual HP device test establishes HP-specific compatibility. An ARM64 build alone proves neither runtime compatibility nor NPU acceleration.
