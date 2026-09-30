# Testing ThreadBack

Run commands from the repository root on Windows. Prepare the SDK with `scripts/Setup.ps1 -Component Sdk`; inference checks also need the corresponding local runtimes and models. Shared fixtures must be synthetic.

## Automated checks

```powershell
.\scripts\Run.ps1 -Rebuild -BuildOnly
.\scripts\Test.ps1
```

The default check runner uses a temporary store and checks protected task/attachment persistence, validation, source links, editing, deletion, context selection, and related structural behavior. It does not run inference or establish semantic accuracy.

Optional checks, when relevant model work is authorized and prerequisites are installed:

```powershell
.\scripts\Test.ps1 -Model
.\scripts\Test.ps1 -Evaluation
.\scripts\Test-HardwareBackends.ps1 -Device Gpu
.\scripts\Test-HardwareBackends.ps1 -Device Npu
```

`-Model` checks generation and cancellation. `-Evaluation` uses twelve scenarios in `tests/fixtures/capsules.json`; these have been viewed during development and are not a blinded accuracy benchmark. Review statements as well as structural/keyword checks.

GPU checks require the optional Vulkan runtime on x64. NPU checks require OpenVINO and suitable hardware; a successful text check writes the app's validation marker. This does not validate Qualcomm acceleration or NPU image inference. Model checks can take substantial time and memory.

Save logs and evaluation reports in ignored `artifacts/verification/`. Record device, backend, runtime/model versions, exact command, exit status, and whether timing includes startup. Never publish private task text or recordings with results.

## Manual acceptance

Use a new synthetic task rather than personal work:

1. Enter a goal, a decision, an open question, and a next step. Add a harmless screenshot with a context note; review OCR and optional image description.
2. Import a short synthetic voice memo. Separately test Record/Stop and confirm recording starts only after Record. Review transcription. `scripts/New-VoiceFixture.ps1` creates a synthetic WAV without using the microphone.
3. Generate a capsule, inspect source links, and compare every statement with its sources. Edit wording, move a statement, and save.
4. Quit through the tray, reopen, and confirm task, sources, edits, and section changes persist.
5. Export Markdown and inspect the unencrypted file. Delete the task and confirm it leaves the library; the exported copy remains separate.
6. Test Cancel and unloading. Check backend labels against actual worker settings rather than hardware presence alone.
7. Open and drag the overlay. Test Snapshot now and Eye on harmless content. Check indicators, retention, and stopping on task changes/lock. Confirm main-window close hides to the tray and Quit exits.

For installer changes, check an extracted path containing spaces, first-time and cached setup, interrupted downloads, missing prerequisites, failure logs, shortcut creation, and launch. Clean-PC, ARM64, GPU, and installed-MSIX checks require their own environments.

## Status and interpretation

Historical checks include storage/source validation, synthetic inference, and cached x64 installation. These do not establish universal hardware support, live microphone acceptance, network isolation, supported installed-package OCR, or production readiness. Screenshot-heavy real-task generation has an unresolved timeout investigation; a synthetic pass is not its resolution.

Maintainers with the local archive must preserve failure counts across sessions. The screenshot-heavy capsule-generation investigation reached its stop limit and requires explicit authorization to resume. Listing optional checks here does not authorize resuming a stopped investigation.
