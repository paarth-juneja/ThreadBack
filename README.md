# ThreadBack

A local Windows app for handing an unfinished task back to your future self.

## Try it on this computer

Double-click **ThreadBack.cmd**. Choose **Try an example**, then **Create my resume capsule**. The local model drafts a chronological account of what happened, plus next steps, decisions, current state, and open questions. Each statement links back to its saved source. Check the wording before relying on it. Close and reopen the app to resume the saved task.

You can add your own text, PNG/JPEG screenshots, or a WAV/M4A voice memo of up to 60 seconds. The Record button uses your microphone only after you click it. Review OCR and transcription text before generation. For a screenshot, add a short **What were you doing here?** note when the image alone cannot show your purpose. Edit wording directly, or use **Move…** to correct a statement's section. Old capsules have a **Regenerate from saved sources** button for the new activity view. Ctrl+S saves; Ctrl+N creates a task. Use Cancel in the bottom status area to stop AI work.

When the optional local vision model is installed, each screenshot also has **Describe image locally**. It adds a brief AI observation of visible content and keeps the image on this PC. Check that description; the image alone cannot establish what you intended or completed. Install the roughly 2.8 GB model and projector with `scripts/Setup.ps1 -Component Vision`. This runs on CPU and is separate from the faster OCR path.

After generation, choose **Unload AI from RAM** to release the local model without closing ThreadBack. The saved capsule and evidence remain available; the next generation reloads the model and may take longer.

## Setup on another Windows PC

Download this repository as a ZIP and **extract it first**, or clone it. Open the extracted project folder and double-click **Install-ThreadBack.cmd**. Keep the setup window open. It installs the local .NET SDK and Desktop Runtime, text AI, voice transcription, and image understanding, restores application libraries, builds ThreadBack, adds a desktop shortcut, and opens the app. No Python installation or API key is needed for these local features. If the Microsoft Visual C++ runtime needed by the AI workers is missing, setup downloads the [official Microsoft installer](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist), verifies its Microsoft signature, and installs it; Windows may ask for administrator approval. If it requires a restart, restart Windows and rerun setup.

Use Windows 10 version 2004 or later, or Windows 11, on x64 (Intel/AMD) or ARM64. Allow roughly **6 GB of downloads and at least 12 GB of free disk space**. 16 GB RAM is recommended for the included models; this is not a guarantee of performance on every PC. Setup needs internet access to Microsoft, NuGet, GitHub, and Hugging Face. Completed downloads are checksum-verified and reused; interrupted transfers keep partial data for resuming. Logs are saved under `artifacts/verification/install-*.txt`. If setup fails, read the error and log before retrying.

For command-line setup or optional settings:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-ThreadBack.ps1
# Smaller setup: text + voice, without the image model
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-ThreadBack.ps1 -SkipVision
# Optional GPU runtime on Windows x64; select GPU in Model settings afterward
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-ThreadBack.ps1 -EnableGpu
```

After setup, use **ThreadBack.cmd** or the desktop shortcut. Keep the project folder in place; if you move it, rerun setup to refresh the shortcut. `-NoLaunch` finishes without opening the app; `-NoShortcut` skips the desktop shortcut. Setup uses CPU by default. NPU installation/testing is separate and remains unverified; the installer does not enable it.

### How the AI connects to ThreadBack

Everything is installed inside the extracted project folder. The launcher sets `THREADBACK_ROOT` to that folder, and the app finds the following files automatically:

| Feature | Runtime | Model |
| --- | --- | --- |
| Resume capsule generation | `.tools/llama/llama-server.exe` | `models/Qwen3-4B-Q4_K_M.gguf` |
| Image understanding | Same llama.cpp runtime | `models/vision/Qwen2.5-VL-3B-Instruct-Q4_K_M.gguf` and its `mmproj` projector |
| Voice transcription | `whisper-cli.exe` inside `.tools/whisper` | `models/ggml-base.en.bin` |

ThreadBack starts the required local worker when you use a feature. Text and image workers communicate with the app through authenticated connections on `127.0.0.1`; voice runs as a local process. There is no manual server configuration. Models, prompts, images, and recordings are processed on the PC. Windows supplies OCR and audio decoding. The .NET SDK includes the Desktop Runtime, and the launcher uses the project-local .NET host, so a separate system-wide .NET install is not required for this setup route.

The standalone MSIX is a separate packaging route and does not include these model downloads. A self-contained package can be built with `./scripts/Package.ps1 -SelfContained` when runtime package downloads are available.

## What is implemented

- Local task library and title search.
- Editable goals, notes, screenshot previews, OCR, voice import/recording and transcription.
- AI handoff with a chronological activity view and four sections: decisions, unresolved questions, last state, and next steps. The model paraphrases selected passages into short descriptions, while exact source excerpts remain attached for review. Up to three statements appear in each section; all evidence remains available for inspection.
- Source ID validation and exact-quote matching. These checks establish provenance, not semantic truth.
- User editing, source inspection, manual-source links, cancellation, and Markdown export.
- DPAPI-protected storage for capsule text and embedded images/audio.
- x64/ARM64 build targets and pinned CPU runtime downloads for both architectures.

## Boundaries

This is a prototype. Snapdragon NPU execution and HP device compatibility have not yet been measured. CPU execution is labeled in the app. Native microphone capture needs a user acceptance test; automated voice tests use a synthetic WAV file. Windows OCR passed a local unpackaged smoke test, but Microsoft's supported desktop deployment requires package identity. The signed MSIX exists; local installation requires trusting its development certificate at the machine level. Do not describe it as an installed or production-signed package.

Optional Eye mode captures the primary screen at your chosen interval. A brief, click-through light-blue glow appears around the primary screen after each automatic capture; it is not included in that screenshot. Manual snapshots do not show the glow. There is no settings switch for the glow yet. No automatic browser/application control occurs. Model setup requires internet, but generation and transcription use local workers. An independent network-isolation test remains part of release validation. Export produces an unencrypted file outside the app's protected store. DPAPI protects data at rest for the current Windows account, not against software running as that same account.

## Verify

```powershell
./scripts/Test.ps1                 # Storage and structural validation
./scripts/Test.ps1 -Model          # Real local generation and cancellation
./scripts/Test.ps1 -Evaluation     # 12 synthetic scenarios with source quotes
```

Results live under `artifacts/verification`. Keyword omission checks are review aids, not accuracy measurements. No benchmark here is a Snapdragon result unless its report explicitly names a Snapdragon device and execution provider.

## Qualcomm device check

Open your reserved Windows device in Qualcomm Device Cloud and run `scripts/Inspect-QualcommDevice.ps1` there. It prints hardware/runtime information without credentials. Send that output back so the exact deployment script can be matched to the device.

AI Hub provides compilation, profiling, and inference jobs for individual models. Device Cloud provides remote device sessions. A model profile is not evidence that the full application was tested.

## Project layout

- `src/ThreadBack.Core`: capsule types, validation, protected storage, local generation.
- `src/ThreadBack.App`: desktop interface, OCR, voice, evidence review.
- `tests`: reproducible checks and synthetic evaluation scenarios.
- `scripts`: setup, launch, package, and target-device checks.
- `docs`: proposal and recording materials.

## Licenses and attribution

ThreadBack's application code is distinct from its third-party models and runtimes. See `THIRD_PARTY.md`. No claim of exclusive ownership is made over third-party software or models.

## Overlay and Eye mode

Use **Show overlay** in the sidebar. Click the floating logo to expand it, or drag it to move it. Closing the main window now hides it to the system tray; use the tray menu's **Quit** to exit. Quick updates save directly to the current task.

**Snapshot now** captures the primary screen, briefly hiding ThreadBack. **Eye** captures on a 1–120 minute interval while the current task is active. Eye starts off, and stops on task changes, session lock, suspend, capture failure, or Quit. Turn it off when you finish a task. Changing the interval/retention requires turning Eye off first. The visible dot and tray tooltip indicate when Eye is on.

Choose to retain 1–30 automatic snapshots; older automatic snapshots in that task are deleted on the next capture. Manual snapshots and updates remain until removed from the source list. Captures and extracted text use the existing encrypted task store. Screen observations are not proof of progress. Generate a handoff in the main app to classify recent sources locally; the model uses up to 20 whole recent sources within its text budget. Full retained history stays available for review. Direct visual understanding and voice-triggered capture are not implemented.


### Overlay fixes and generation limits

The floating logo supports drag-to-move and has a visible **×** in both collapsed and expanded views. × hides the overlay and pauses Eye; reopen it from the sidebar or system tray.

Generation prioritizes your notes and updates, removes duplicate passages and short OCR labels, and selects at most 24 passages (one from each of at most six screenshots), within 4,200 characters. Large OCR results are clipped to an exact 900-character excerpt for generation; the original source stays encrypted and available for review. The status shows loading/organizing stages and elapsed time; requests stop after 90 seconds. The same local 4B model remains in use, so CPU generation is not instantaneous.
