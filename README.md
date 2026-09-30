# ThreadBack

ThreadBack is a Windows app that helps you resume unfinished work. Save notes, screenshots, and voice memos in a task, then use local AI to draft an activity timeline, decisions, open questions, current state, and next steps. Inspect the linked sources and edit the draft before relying on it.

## Install and launch

1. Download this repository as a ZIP and extract it, or clone it.
2. Double-click **Install-ThreadBack.cmd**. Keep the setup window open until it finishes.
3. Use **ThreadBack.cmd** or the desktop shortcut to open the app again. Keep the project folder in place; rerun setup if you move it.

Setup downloads the project-local .NET SDK/Desktop Runtime, AI runtimes, and models, builds the app, and opens it. No API key or Python installation is needed. If required, setup verifies and installs Microsoft's Visual C++ runtime; Windows may request administrator approval or a restart.

Requirements: Windows 10 version 2004 or later, or Windows 11; x64 (Intel/AMD) or ARM64. Allow roughly 6 GB of downloads and at least 12 GB of free disk space. 16 GB RAM is recommended. Setup needs internet access to Microsoft, NuGet, GitHub, and Hugging Face. Completed downloads are checksum-verified; interrupted transfers can resume. Logs are under `artifacts/verification/install-*.txt`.

For a smaller installation or optional GPU support, run from the extracted project folder:

```powershell
# Text and voice, without the image model (Windows OCR remains available)
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-ThreadBack.ps1 -SkipVision
# Optional Vulkan GPU runtime on Windows x64
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-ThreadBack.ps1 -EnableGpu
```

Setup uses CPU by default. After GPU setup, select GPU in **Model settings** if a usable device is detected. `-NoLaunch` and `-NoShortcut` are also available.

## Your first handoff

Choose **Try an example**, then **Create my resume capsule**. Inspect a source link, edit a statement if needed, save, and reopen the task later.

For your own task, enter a goal and add notes, PNG/JPEG screenshots, or a WAV/M4A voice memo up to 60 seconds long. Review OCR and transcription text. Add a short explanation of what you were doing when a screenshot alone cannot establish your intent. With the optional image model installed, **Describe image locally** or **Describe all screenshots** adds observations before generation.

Edit generated wording, use **Move…** to change its section, search saved tasks, and export a Markdown handoff. Ctrl+S saves; Ctrl+N creates a task. Use Cancel to stop AI work and **Unload AI from RAM** to release a loaded model. Model settings offer memory caps and idle unloading; caps do not limit total system memory use.

## Overlay and screen capture

Choose **Show overlay** to open the floating controls. Drag the logo to move it; click it to expand. **Snapshot now** captures the primary screen. **Eye** takes automatic snapshots at a selected 1–120 minute interval, retaining 1–30 automatic snapshots per task. Older automatic snapshots are deleted on the next capture; manual snapshots remain until removed.

Eye starts off and stops on task changes, session lock, suspend, capture failure, or Quit. Its indicator and tray tooltip show when it is active. Automatic captures briefly display a light-blue screen-edge glow. The overlay's **×** hides it and pauses Eye. Closing the main window hides the app to the tray; choose **Quit** there to exit.

## Privacy and local AI

Generation, image descriptions, and transcription use workers on this PC. Text and image workers use authenticated localhost connections; voice uses a local process. Windows provides OCR and audio decoding. Recording starts only when you click Record. Eye captures the primary screen when enabled, so consider what is visible before turning it on.

Saved task text and embedded images/audio use Windows DPAPI protection for the current account. This does not protect against software running as that account. Markdown exports are unencrypted files outside the protected store. Independent network-isolation validation remains pending.

The launcher sets `THREADBACK_ROOT`; models and tools live beside the source in ignored `models/` and `.tools/` folders. Text uses Qwen3 4B with llama.cpp; images use Qwen2.5-VL 3B with its projector; voice uses Whisper Base English with whisper.cpp. See [third-party components and licenses](THIRD_PARTY.md), including the image model's research license restrictions.

## Current limitations

ThreadBack is an early app. Source links and exact excerpt checks establish provenance, not the truth or quality of interpretations. Drafts can omit or misclassify information. Large screenshot-heavy tasks have encountered generation timeouts; a broadly validated fix remains pending. Keep concise progress notes and review output.

The installer has passed a cached x64 check on the development PC. Fresh installation on a clean PC, ARM64 installation, live microphone acceptance, and supported installed-package OCR validation remain pending. Optional Intel OpenVINO NPU text execution requires a successful local validation marker; image understanding stays on CPU in that mode.

The packaging script creates a development-signed MSIX, with optional self-contained .NET runtime. It does not bundle AI models or provide a production-trusted installer.

## License

ThreadBack's application code and documentation are licensed under the [MIT License](LICENSE), Copyright (c) 2026 Paarth Juneja. Third-party models, runtimes, and libraries retain their own licenses; see [THIRD_PARTY.md](THIRD_PARTY.md).

## Contribute

See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and [docs/TESTING.md](docs/TESTING.md) for automated and manual checks.

- `src/ThreadBack.App/`: interface, OCR, voice, overlay, and hardware settings.
- `src/ThreadBack.Core/`: task types, protected storage, source validation, and inference.
- `tests/`: checks and synthetic evaluation fixtures.
- `scripts/`: installation, downloads, launch, packaging, and verification.

Downloaded tools, models, build output, private captures, local development archives, and submission materials are excluded from Git.
