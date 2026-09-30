# Qualcomm AI Hub Whisper profiling: failure report

## Latest discriminator: direct decoder binary succeeds

After a separate explicit authorization, direct decoder job `j5qlv3kep` (model `mq9yw080n`) succeeded on Snapdragon X Elite CRD using `--qairt_version 2.45 --compute_unit npu --max_profiler_iterations=1`. One measured invocation took 5,245 microseconds; all 975 execution-detail entries report NPU. Original binary hash was rechecked before the one submission; no binary modification or automatic retry. Raw profile and summary are under `artifacts/verification/aihub-whisper/decoder-binary-j5qlv3kep-*.json`; job record remains in `jobs.json`.

There are still exactly six failed ONNX attempts, now alongside two successful direct binary profiles (encoder and decoder). Both components can execute through the direct QNN path; ONNX/EP/service-path root cause remains unknown. This is neither full transcription validation nor a stable decoder latency benchmark. No further jobs submitted. Qualcomm's official support address is `ai-hub-support@qti.qualcomm.com`, verified at https://workbench.aihub.qualcomm.com/docs/contact.html; support draft updated and not sent. Earlier statements that decoder execution is unverified are superseded only for direct component profiling.

## Update: explicitly authorized sixth ONNX attempt

The user authorized one repaired encoder ONNX check after the offline investigation. Job `jpe7yk985` (model `mq26x250n`) also **FAILED** with `Failed to profile the model: unexpected device error`. Total: **six failed ONNX profile jobs**, plus the separate successful direct encoder binary job. No seventh attempt was submitted. Remote testing is stopped again.

Goal: confirm whether adding the missing Microsoft-domain opset import fixes encoder profiling. The new package retained IR 13, tensor definitions and byte-identical QNN context bytes. Its only model change from the earlier corrected-basename encoder was `com.microsoft` opset 1. ONNX 1.21.0 structural checks passed before submission. This proves the local structural fix is insufficient to resolve the remote failure.

Submitted `.tools/qualcomm-whisper/prepared-validated/encoder.onnx` to Snapdragon X Elite CRD using exactly `--runtime_debug=true --max_profiler_iterations=1 --onnx_execution_providers=qnn --qairt_version 2.45`, matching failed debug job `jgzlnrqx5`. Package fingerprint: `1136f615123433fa6f83a4e0ff73ac7c952e0de29cb3e0635cbb0a56c0194428`. Submission time: `2026-09-22T23:10:43.662287+00:00`; failure observed at `2026-09-22T23:16:18.695753+00:00`.

Commands: `.tools/aihub-python/Scripts/python.exe scripts/audit_qualcomm_whisper.py` (passed), `scripts/Invoke-AIHubProfile.ps1 -Action submit-repaired-encoder` (once), and repeated read-only `-Action status` checks until terminal failure. Added the dedicated action to `scripts/aihub_profile.py` and its PowerShell wrapper; it stores a separate `encoder-onnx-repaired` entry and preserves the successful encoder binary record. No automatic retry was enabled.

Raw evidence: `artifacts/verification/aihub-whisper/jobs.json` and `artifacts/verification/aihub-whisper/encoder-onnx-repaired-jpe7yk985-logs/jpe7yk985_runtime.log`. The new log again contains only SOC SC8380XP / Hexagon 73 / QNN / FP16 detection and HTP core capability, with no underlying exception. The job progressed through CREATED, PROVISIONING_DEVICE and MEASURING_PERFORMANCE before failure.

Confirmed: repaired wrapper remains unsuccessful remotely; original direct encoder binary success remains valid. Unresolved: service staging, ORT/QNN plugin/backend configuration, context loading/binding, worker fault or another device/runtime issue. The generic log cannot distinguish these. Decoder and full transcription remain unverified. Original assets and prior evidence are retained; API key is private; slides and proposal are unchanged. The support draft includes this sixth job and has not been sent. The escalation prompt below is maintained separately in `docs/AIHUB_ESCALATION_PROMPT.md`.

The remainder of this report records the original five-attempt checkpoint.

Date: September 23, 2026 (Asia/Kolkata). This report was created after five failed remote profile jobs in the single AI Hub profiling step. No further jobs should be submitted for this step unless the user explicitly asks to resume.

## Intended result

Profile Qualcomm's official Whisper-Base precompiled QNN ONNX encoder and decoder on a Windows 11 Snapdragon X Elite CRD, then inspect raw latency and compute-unit evidence before considering application integration. This would be a component test, not a full ThreadBack application test or speech accuracy result.

## Source and local setup

- Official source archive: `https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/whisper_base/releases/v0.62.2/whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite.zip`
- Saved original ZIP: `.tools/qualcomm-whisper/whisper-base-x-elite.zip`, 180,777,567 bytes, SHA-256 `026660D55287F98BD37D53B461BB0604661E0427BE6113E691EFC9300309C798`. ZIP CRC test passed.
- Archive metadata: precompiled QNN ONNX, QAIRT `2.45.0.260326154327`, ONNX Runtime `1.27.1`, Snapdragon X Elite / SC8380XP / Hexagon v73. AI Hub currently lists QAIRT 2.45 as its default. The authenticated device list includes `Snapdragon X Elite CRD`, Windows 11 ARM64, ONNX and QNN.
- Encoder input: `input_features` float16 `[1,80,3000]`, 12 cache outputs. Decoder: 27 inputs and 13 outputs. The current ThreadBack baseline uses local Whisper Base English via CPU; this official model is multilingual, so a performance comparison would need care.
- Local Python client: `qai-hub==0.55.0`, `onnx==1.19.1` in `.tools/aihub-python`. The API key is protected outside the project; never print or request it.
- The prepared wrappers load with `onnx.load`, but the installed `onnx==1.19.1` checker cannot validate their IR version 13 because that checker supports up to IR version 12. This is a local checker limit, not proof the model is invalid; a newer checker or byte-level comparison may help inspect the rewrite.

## Completed attempts

| # | Component | Job | What changed | Result |
|---|---|---|---|---|
| 1 | Encoder ONNX | `jp0m6l9ng` | Uploaded original archive file names in one `.onnx` directory | Failed: `ONNX model and QNN context binary file must have same basename.` |
| 2 | Encoder ONNX | `j5wl4qrmp` | Renamed binary `encoder.bin` and rewrote ONNX `EPContext` reference to `./encoder.bin` | Failed: `Failed to profile the model: unexpected device error` |
| 3 | Decoder ONNX | `jgk283kng` | Renamed binary `decoder.bin` and rewrote ONNX `EPContext` reference to `./decoder.bin` | Same unexpected device error |
| 4 | Encoder ONNX | `jgzlnrqx5` | Reused uploaded model; one iteration, runtime debug, explicit QNN provider and QAIRT 2.45 | Same unexpected device error |
| 5 | Decoder ONNX | `jprlwem7p` | Same debug settings for decoder | Same unexpected device error |

The corrected ONNX jobs got as far as the service's `MEASURING_PERFORMANCE` state. Each available runtime log only reports detection of SC8380XP, Hexagon v73, QNN support, unsigned PD support, and FP16 support. The logs do not show the underlying error. The first naming error was concrete and fixed; the subsequent ONNX root cause remains unconfirmed.

A direct encoder `.bin` profile, job `j568wnxyg`, was submitted before the fifth failure was observed and **succeeded**. It requested QAIRT 2.45 and NPU. Its raw profile is `artifacts/verification/aihub-whisper/encoder-j568wnxyg-profile.json`: 100 measured invocations, median `49,304.5` microseconds (49.3 ms), minimum `48,985` and maximum `50,970` microseconds. All 556 `execution_detail` entries report `compute_unit: NPU`. This establishes an encoder component result on the Snapdragon X Elite CRD. It does not establish decoder performance, end-to-end transcription latency/accuracy, application integration, or HP-device compatibility. There was no further cloud submission after the fifth failed job.

## Evidence and implementation

- `artifacts/verification/aihub-whisper/jobs.json`: non-secret model IDs, job IDs, statuses, options and error messages; previous attempts remain in `history`.
- `artifacts/verification/aihub-whisper/*-logs/`: raw runtime logs.
- `scripts/download_qualcomm_whisper.py`: checked public archive download.
- `scripts/prepare_qualcomm_whisper.py`: model split and ONNX context filename rewrite.
- `scripts/aihub_profile.py` and `scripts/Invoke-AIHubProfile.ps1`: private-token loading, upload, job submission, duplicate prevention, status/profile retrieval.
- `artifacts/verification/aihub-devices.json`: available device attributes.
- [Qualcomm profiling docs](https://workbench.aihub.qualcomm.com/docs/hub/profile_examples.html), [framework version selection](https://workbench.aihub.qualcomm.com/docs/hub/frameworks.html), and [failure guidance](https://workbench.aihub.qualcomm.com/docs/hub/faq.html).

## Next investigation

The direct `.bin` success narrows the investigation: the encoder context binary can run on the device, while both ONNX wrapper jobs failed. Compare the archive's ONNX wrapper against AI Hub's expected precompiled ONNX packaging and service runtime configuration. Determine whether the generic error comes from the ONNX Runtime execution provider, packaging, memory pressure, or a Workbench service fault. Do not infer a cause from the generic error alone. A decoder `.bin` profile is the smallest next remote test, but the user's failure limit requires explicit direction to resume this step. The participant can contact Qualcomm AI Hub support with job IDs if the logs remain uninformative; no message has been sent.

Keep the existing CPU application path and draft submission materials unchanged while this is unresolved. The user requires stopping a step after its fifth failed attempt and giving a detailed report and escalation prompt. That rule is in root `AGENTS.md`.
