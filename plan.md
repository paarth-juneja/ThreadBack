# ThreadBack — project plan and session handoff

Last updated: September 28, 2026 (Asia/Kolkata). Deadline supplied by participant: September 30, 2026. Confirm the exact submission cutoff/timezone before uploading.

**September 28 checkpoint:** The participant explicitly authorized resuming the stopped Comsys check. The full local visual sequence now runs with reduced inference-image size and longer deadlines; five task-relevant screenshots were described and a six-entry real-source handoff was generated without altering the encrypted capsule. Images alone yielded a safe but generic timeline. A separate in-memory comparison using the participant's own explanations produced a useful timeline and correctly placed broken code under open issues, with no invented decision or next action. The explanations were not written back to the task. Release build and 17 checks pass; participant UI and factual acceptance remain next. See `WORKING_NOTES.md` and `artifacts/verification/comsys-user-context-final-2026-09-28.json`.

**September 27 historical checkpoint:** The real model check stopped after five `IOException` visual-description failures under `AGENTS.md`; details are retained in `docs/REAL_COMSYS_ACCEPTANCE_FAILURE_REPORT.md`. The participant's September 28 authorization permitted the successful resumed check above. The earlier Code Integrity finding did not prove every local runtime launch was blocked.

**September 26 checkpoint:** In response to the participant's Comsys Lab Viva test, the app now drafts an evidence-linked activity timeline and paraphrased handoff, with screenshot context and foreground window titles. A synthetic Comsys example and 16 core checks passed; the participant's real saved capsule was not regenerated. The participant explicitly authorized resuming the stopped vision-model download. The full main model and projector are now installed under `models/vision/`, and both match expected sizes and SHA-256 checksums. Optional local image-description code is built, but visual inference and the real Comsys capsule remain untested at the participant's request. See `WORKING_NOTES.md` and `artifacts/verification/vision-download-complete-2026-09-26.json`. Continue functional checking only when the participant is ready.

**Current status:** local Windows prototype built and tested with working CPU transcription. A September 23 fictional circuits acceptance pass covered note generation, screenshot import/OCR, synthetic WAV import/transcription, evidence inspection, edits, category movement, save/relaunch/reopen, export and deletion. A participant-provided 13.3-second recording exposed a recognition error in the first phrase (“nodal analysis for problem three”); the later dependent-source and class-key content was retained. Transcript review remains essential. Both direct QNN component profiles succeeded on Snapdragon X Elite CRD: encoder `j568wnxyg` (100 runs, median 49.3045 ms, 556/556 execution details NPU) and decoder `j5qlv3kep` (one measured invocation, 5.245 ms, 975/975 details NPU). Six ONNX profiles failed, including the structurally repaired encoder. Full NPU transcription, Snapdragon app integration and HP compatibility remain unverified.

**Current decision:** continue app acceptance and demo preparation using the working CPU backend. ONNX troubleshooting is paused and does not block the rest of the project. The participant reports sending the support message to `ai-hub-support@qti.qualcomm.com`; no reply has been provided in this task. The saved message is `docs/AIHUB_SUPPORT_DRAFT.md`. No more cloud jobs are authorized.

This is the main status and next-session plan. Keep incremental technical observations in WORKING_NOTES.md. Do not regenerate the presentation, PDF, or full reports after each step; update submission materials together near the end.

## Goal and scope

ThreadBack helps a student resume interrupted work using a deliberate handoff: selected notes, screenshots, or a voice memo become decisions, unresolved questions, last state, and next steps. AI classifies source passages; the application preserves their original wording and evidence links. Users review, edit, or move passages between sections.

Target: a Windows application intended for Snapdragon-powered HP PCs. The first planned NPU optimization is speech transcription. Keep the existing CPU language-model backend until another backend is actually implemented and verified.

Participant time budget: 6–10 hours total alongside midsems. Assistant handles engineering, research, troubleshooting and preparation. Participant handles private account steps, short remote-device handoffs, factual review, recording, and submission. No mandatory courses, Arduino, continuous capture, cloud sync, or automatic command execution in the prototype.

## Completed

- [x] Chosen project concept, differentiation, scope and example workflow.
- [x] WPF/.NET Windows app with task library, selected notes/screenshots, OCR, voice import/recording implementation, and CPU transcription.
- [x] Local Qwen3 4B Q4_K_M through llama.cpp; exact source-wording extraction, evidence inspection, editable text, and manual category movement.
- [x] Windows-account-protected capsule storage, attachments, save/reload, export and deletion.
- [x] Ten functional checks passed, plus real-model source preservation and cancellation checks.
- [x] Twelve synthetic cases generated structurally valid capsules. Semantic/category imperfections are documented; this is not an accuracy certification or untouched held-out evaluation.
- [x] Local OCR and synthetic WAV transcription smoke tests passed. Live microphone acceptance remains pending.
- [x] September 23 local CPU example rerun passed source/category checks; reproducible OCR and synthetic WAV checks passed again. Results are in `artifacts/verification/demo-acceptance-2026-09-23-summary.json`, and the participant acceptance steps are in `docs/LOCAL_ACCEPTANCE.md`.
- [x] Participant-provided M4A recording imports directly after adding Windows Media Foundation decoding. Recognition was partial; the first phrase needs manual correction. The original audio stayed outside the repository.
- [x] x64 application and x64/ARM64 signed development packages built. ARM64 is only cross-built so far.
- [x] Existing draft presentation (eight slides), proposal, demo script and judge Q&A saved physically in the project. Presentation file checks and visual review completed for that draft.
- [x] Read participant's nine-page `Get Started - Qualcomm AI Hub.pdf`.
- [x] Installed project-local AI Hub client (`qai-hub==0.55.0`) and created private key-entry/device-discovery helper.
- [x] Participant ran Connect-AIHub.cmd successfully. API returned **78 device entries**; results saved in `artifacts/verification/aihub-devices.json`.
- [x] Verified Windows 11 / ARM64 entries for Snapdragon X Elite CRD, Snapdragon X Plus 8-Core CRD, and Snapdragon X2 Elite CRD, with ONNX and QNN attributes.
- [x] Downloaded the official Whisper-Base Snapdragon X Elite model asset. Both direct component binary profiles succeeded, with raw NPU evidence saved. ONNX remains unresolved after six failed attempts.

## Pending, in priority order

### 1. Complete local acceptance and prepare the CPU demo

- [ ] Test a real study handoff. The fictional circuits acceptance pass covered generation, evidence inspection, edits, category moves, in-app screenshot/OCR and synthetic WAV import/transcription.
- [x] Test participant-provided spoken audio with the CPU transcriber. The 13.3-second M4A now imports directly into the app; the first phrase was misrecognized while the later dependent-source/class-key content was retained. Evidence: `artifacts/verification/participant-speech-2026-09-23.json`. The participant confirms the app's Record button works; exact-word accuracy from an in-app spoken recording remains unmeasured. The app now rejects Whisper's `[BLANK_AUDIO]` marker. Keep the synthetic WAV for reproducible transcription checks.
- [x] Verify save, close/relaunch, reopen, export and deletion with the isolated fictional circuits example. Export remained after capsule deletion; details are in `artifacts/verification/interactive-acceptance-2026-09-23.json`.
- [x] Review this example's category placement and source preservation; fix the observed silent-audio marker bug. Broader semantic acceptance remains pending.
- [ ] Rehearse the prepared deterministic 2–3-minute demo using the working CPU transcription and CPU language-model backend. The updated `docs/DEMO_SCRIPT.md` labels the actual backend and component-only NPU profiles accurately.
- [ ] Record results in `WORKING_NOTES.md` and `artifacts/verification/`. Update submission materials together near finalization.

Proceed without waiting for Qualcomm support. Remote Snapdragon/HP app execution still requires separate validation.

### Deferred: AI Hub ONNX/NPU integration — not on the demo's critical path

- [x] Download and ZIP-validate the official Whisper-Base precompiled QNN/ONNX asset for Snapdragon X Elite; SHA-256 is in `WORKING_NOTES.md`.
- [x] Inspect archive: encoder and decoder model packages, QAIRT 2.45/ONNX Runtime 1.27.1, tensor requirements.
- [x] Implement `scripts/aihub_profile.py` and safe job-ID recording/status retrieval.
- [x] Verify QAIRT 2.45 service support. Six ONNX profiles failed; both direct component `.bin` profiles succeeded.
- [x] Save raw encoder profile and compute-unit evidence: 100 runs, median 49.3 ms, 556/556 listed details on NPU. This is a component result only.
- [x] Save decoder evidence: one measured invocation, 5.245 ms, 975/975 execution details NPU. This is not a stable latency benchmark.
- [x] Fix missing Microsoft-domain opset imports in separate local wrappers; structural checks pass but repaired encoder profiling still fails.
- [x] Participant reports sending the Qualcomm support message.
- [ ] Review support feedback when the participant supplies it. No inbox monitoring or automatic follow-up is scheduled.
- [ ] Resume remote troubleshooting only with actionable evidence and explicit participant authorization. Preserve the cumulative failure count; do not repeat speculative jobs.
- [ ] Once a runtime path works, validate decoder sequencing/caches, speech accuracy and end-to-end latency before app integration. Component profiling alone does not establish working NPU transcription.
- [ ] Record results in WORKING_NOTES.md and raw artifacts only; keep slides/reports unchanged for now.

Download checkpoint: `.tools/qualcomm-whisper/whisper-base-x-elite.zip` is complete and passed ZIP integrity validation (180,777,567 bytes). The source archive remains unchanged; previous packages are under `.tools/qualcomm-whisper/prepared/`, and structurally repaired copies are under `.tools/qualcomm-whisper/prepared-validated/`.

Allowed current claim: **encoder and decoder components profiled on Snapdragon NPU**. Do not claim that ThreadBack transcription runs on NPU. The failure report and escalation prompt remain in `docs/`.

Source: https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/whisper_base/releases/v0.62.2/whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite.zip

Qualcomm's model listing identifies this asset as QAIRT 2.45 / ONNX Runtime 1.27.1. Confirm compatibility with the actual profiling service before using those options. This Qualcomm asset is multilingual Whisper-Base; the current local baseline is Whisper Base English. They are not automatically a controlled like-for-like performance comparison.

### 2. Prepare a Windows Device Cloud session

- [ ] Verify the participant's actual minutes balance, expiry, device entitlement, and whether the displayed allocation is shared or per device. Participant reports approximately **1,000 free minutes**, but that scope is unconfirmed. The supplied screenshot shows “Unlock Free Minutes,” not a numeric balance.
- [ ] Prefer Snapdragon X Elite Windows initially to match the prepared model; X2 Elite is an alternative requiring compatible assets and runtime checks. Avoid a Linux board or phone for the full WPF app test.
- [ ] Prepare the complete ARM64 app, matching native workers, required Desktop Runtime, models, synthetic test inputs, and exact short commands before reserving time.
- [ ] Run a short first session to inspect hardware, transfer/install, open the app, generate, inspect evidence, save, restart and reopen.
- [ ] Test OCR and voice-file import. Confirm remote microphone support separately; use the synthetic WAV for a reproducible fallback.
- [ ] Validate the working CPU app path first. Deferred NPU integration is not a prerequisite for this session; label the actual backend.
- [ ] Export all logs/artifacts before ending the session; ordinary cloud devices are cleaned between sessions.

The AI Hub key does not establish Device Cloud API authentication. Do not reuse it against another service without verifying its authentication instructions.

### 3. Record on the remote device if the workflow is stable

The participant prefers trying the final demonstration on Qualcomm's device, conditional on access and successful app testing. Use the working CPU backend; ONNX support resolution is not a prerequisite. Keep a local CPU recording ready if remote access or ARM64 acceptance is not reliable in time.

- [ ] Rehearse locally first and prepare a deterministic synthetic example.
- [ ] Use an initial 30–60-minute remote validation session as a planning estimate; reserve a separate 30–45-minute recording slot if necessary. Do not allocate all reported free minutes in advance.
- [ ] Capture the actual Windows Snapdragon application workflow and exact device identity. Use available session recording or record the remote view locally, after checking how recording/export works.
- [ ] Keep remote-display/network delay distinct from model inference timing. Label any shortened waits.
- [ ] Describe CRD footage as a Qualcomm reference-device demonstration, not HP-device validation. Actual HP compatibility remains untested unless tested on HP hardware.

Qualcomm's FAQ documents compute-session screen-recording artifacts and Windows remote desktop access. It says minutes are allocated at user or organization level and organizations may share them; it does **not** establish that this account gets 1,000 minutes per device. Portal provisioning/queue time is not counted, but budget work performed after a session starts as session time. Source: https://qdc.qualcomm.com/support/faq

### 4. Finish acceptance and submission materials

- [ ] Participant tests a real study handoff, microphone, edits/moves and close/relaunch.
- [ ] Review remaining category errors; avoid claiming exact quotation checks prove semantic correctness.
- [ ] Resolve or document development-package certificate trust. Local MSIX installation previously failed with 0x800B0109; direct launch is the existing local route.
- [ ] Independent offline/network-isolation check if time permits. General accessibility and larger user evaluation remain future work.
- [ ] Update proposal, PowerPoint and narration once with final measured facts and participant details.
- [ ] Rehearse and record the 2–3-minute demo; review final intake fields and submit once.
- [ ] Retain submission receipt. Nothing has been submitted on the participant's behalf.

## Schedule from this checkpoint

| Target | Milestone | Participant effort estimate |
|---|---|---|
| Sep 23–24 | Local acceptance and CPU demo example; verify Device Cloud entitlement if available | 30–45 min |
| Sep 24–26 | Fix acceptance bugs, rehearse CPU demo; optional prepared remote Windows validation | 60–90 min across guided sessions |
| Sep 26–27 | Freeze working demo; local/remote acceptance and rehearsal | 45–60 min |
| Sep 27–28 | Consolidate final materials and review claims | 30–45 min |
| Sep 28–29 | Record, review and submit | 90–120 min |
| Sep 30 | Deadline buffer only | Reserve 60–120 min if needed |

These are estimates, not booked sessions or background automations. Track participant time already spent against the original 6–10-hour total. Plan around the working CPU backend now and freeze the demo by Sep 26. A support reply must not delay acceptance or submission; include NPU integration only if separately completed and verified before the freeze. If Device Cloud stays inaccessible, keep the local demonstration and clearly separate any AI Hub component results.

## Files and how to resume

| Location | Purpose |
|---|---|
| `plan.md` | Main status, priorities and next-session handoff |
| `WORKING_NOTES.md` | Incremental evidence and decisions; update this during work |
| `ThreadBack.cmd` | Launch the local app |
| `src/`, `tests/`, `scripts/` | App, functional checks, setup and deployment work |
| `artifacts/verification/` | Raw local test results and AI Hub device list |
| `docs/VERIFICATION.md` | Existing local baseline and limitations |
| `docs/PROPOSAL.md`, `docs/DEMO_SCRIPT.md`, `docs/JUDGE_QA.md` | Existing drafts to finalize later |
| `deliverables/ThreadBack-presentation.pptx` | Current presentation draft; older prototype deck is superseded |
| `docs/STAGED_PLAN.md`, `docs/QUALCOMM_RUNBOOK.md` | Earlier planning snapshots; this file takes precedence on status |

The API key is stored with Windows current-user protection outside the project. **Do not print its value, paste it into chat, or add it to source control.** The connection helper can reuse it with `scripts/Connect-AIHub.ps1 -UseSaved`; the participant does not need to enter it again on this Windows account.

**Next-session starting point:** read this file, `AGENTS.md`, and `WORKING_NOTES.md`. Start with local app acceptance and CPU demo preparation in step 1; fix reported bugs and record results. Do not restart AI Hub setup, request the API key or submit remote jobs. Both component binaries have successful NPU profiles; ONNX and full NPU transcription remain unresolved. Read the failure report/escalation prompt when actionable support feedback arrives. No Device Cloud reservation, inbox monitor or background continuation has been started.
