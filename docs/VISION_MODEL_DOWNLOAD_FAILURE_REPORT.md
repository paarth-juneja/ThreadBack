# Local vision model download: stopped September 25, 2026

**Resolved September 26, 2026:** The participant explicitly requested the full download. The saved 1,543,979,319-byte cache partial was copied and resumed from byte 1,543,979,319; the remaining 385,921,737 bytes downloaded successfully in one transfer. The final model is installed under `models/vision/`, with the expected 1,929,901,056-byte size and SHA-256 `d02fe9b69ad8cadbbd228e387667af66612c44bed29ffc8eb1e7caf9ac486c12`. The projector was also rechecked and matched its expected size and hash. See `artifacts/verification/vision-download-complete-2026-09-26.json`. The sections below preserve the September 25 failure state as historical evidence. No image inference or real-capsule checking was performed after the download.

## Goal and state

Add optional local image understanding to ThreadBack so a screenshot can receive a short visual description before the existing resume handoff is generated. The application code is implemented and the latest x64 development build compiles and is signed. The visual model has **not** been installed or tested. The existing OCR, window-title, user-context, paraphrasing, and activity timeline path remains available.

The required Qwen2.5-VL 3B Q4 model is 1,929,901,056 bytes. Its Q8 projector is installed at `models/vision/mmproj-Qwen2.5-VL-3B-Instruct-Q8_0.gguf` (844,757,728 bytes; SHA-256 `980c9b2f78c04e6cff93d277ada09e768394f112d75db3b4e9dea8a69f9fb904`). The model file `models/vision/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf` does not exist yet. The app hides **Describe image locally** until both files exist. The interrupted Hugging Face cache file is 1,543,979,319 bytes at `C:\Users\paart\.cache\huggingface\hub\models--ggml-org--Qwen2.5-VL-3B-Instruct-GGUF\blobs\d02fe9b69ad8cadbbd228e387667af66612c44bed29ffc8eb1e7caf9ac486c12.downloadInProgress`. A separate direct-download partial is 43,316,180 bytes at `models/vision/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf.partial`. Neither partial is a usable model.

## Failure count and attempts

Six unsuccessful transfer attempts were counted conservatively for this one download step. Stop after the fifth failure is required by `AGENTS.md`; no further retry is authorized without an explicit user request. The last three failures happened inside one automatic downloader invocation.

1. Started the bundled `llama-server.exe -hf ggml-org/Qwen2.5-VL-3B-Instruct-GGUF:Q4_K_M` on localhost as PID 16476. It downloaded the projector and most of the model but stalled near 1.5 GB of the model. The process was stopped, leaving the cache partial.
2. Ran `scripts/Setup.ps1 -Component Vision`, which uses resumable `curl.exe`. It reached 43,316,180 bytes at about 0.3 MB/s and was stopped to reuse the larger cache partial.
3. A diagnostic 1 MiB HTTP range request timed out after 30.277 seconds with only 212,992 bytes, despite HTTP 206.
4. Restarted the bundled `llama-server.exe -hf ...` as PID 5308. Its first internal attempt failed: `Failed to read connection (status: -1)`.
5. Its automatic retry failed: `Could not establish connection (status: -1)`.
6. Its second automatic retry failed with the same connection error. The runtime reported `download failed after 3 attempts` and `failed to download model`.

Raw non-secret logs and exact sizes are in `artifacts/verification/vision-download-initial-stderr-2026-09-25.txt`, `vision-download-resume-stderr-2026-09-25.txt`, and `vision-download-state-2026-09-25.json`. No API key was used or exposed, and no Qualcomm job was submitted.

## Code and verification completed before the stop

- `src/ThreadBack.Core/LocalGenerator.cs` now produces short evidence-linked paraphrases, and `ResumeBrief.Activity` holds a chronological account. `src/ThreadBack.App/MainWindow.xaml` shows that account plus an immediate pause/next-step summary.
- Screenshots can carry user context, foreground window title, and an optional local visual description. `ActivityContext` limits noisy OCR sent to generation while preserving the original encrypted source.
- `src/ThreadBack.Core/LocalVision.cs`, the image-description button, and the pinned `scripts/Setup.ps1 -Component Vision` download path are implemented. They have **not** been functionally tested with a visual model.
- Sixteen core checks passed. A synthetic Comsys Lab Viva text-model run produced seven activity entries in 28.4 seconds, including Gmail, Nalanda, PDF, download attempt, and broken-code context. Its output is `artifacts/verification/comsys-handoff-2026-09-25.json`; the resume view preview is `comsys-handoff-preview-2026-09-25.png`. This was synthetic evidence, not the participant's saved task. The final development build compiled and signed after stopping its running process. No visual-model inference was attempted, as the participant requested testing later.

The participant's saved encrypted capsules were not regenerated or edited. No submission slides, proposal, or reports were regenerated.

## Unresolved cause and next gate

The transfer failed at the network/download layer; the exact network cause is unknown. The cache partial may be reusable but is not verified as complete. A future session must first receive explicit user direction to resume this failed download step. Then use the saved partials, verify the full model SHA-256 `d02fe9b69ad8cadbbd228e387667af66612c44bed29ffc8eb1e7caf9ac486c12`, place it under `models/vision/`, and only afterward test the image-description feature. The user asked to leave functional checking and other work for later.
