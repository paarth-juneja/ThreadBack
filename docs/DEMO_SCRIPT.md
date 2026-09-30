# ThreadBack recording script

Target length: 2–3 minutes. Use the built-in offline-search example for the recording. Rehearse once before recording. Present this as the local CPU prototype; Snapdragon application validation is a separate milestone.

## Preparation

1. Open ThreadBack using `ThreadBack.cmd`.
2. Choose **Try an example**. Generate once to warm up the local model and check the result. The September 23 local run took 15.4 seconds after loading; allow for a longer wait on a cold run.
3. Confirm the rejected option appears, the pending RAM test stays unresolved, and each supporting quote opens the right source.
4. If the model misclassifies a statement, correct it openly in the demonstration or use a different truthful example. Do not present manual corrections as untouched AI output.
5. Start a fresh example for recording. Keep private files and notifications out of view. Use the generated sections as observed; if a section changes on the recording run, describe and correct it openly.

## 0:00–0:20 — The problem

**Screen:** Title slide, then ThreadBack.

**Say:** “When I return to a college project after studying for an exam, I can reopen the files. The harder part is remembering why I rejected one approach and what I still need to test. ThreadBack captures that handoff before I leave.”

## 0:20–0:50 — Capture a handoff

**Action:** Choose **Try an example**. Show the goal and both evidence notes.

**Say:** “Here I need an offline search engine. Approach A needs the internet, so I rejected it. Approach B works with a local index, but I haven't checked its memory use. I can add notes, screenshots, or a short voice handoff. I choose what enters the capsule.”

## 0:50–1:25 — Generate locally

**Action:** Click **Create my resume capsule**.

**Say:** “ThreadBack uses a local language model to organize source passages into a handoff. It preserves the original wording so I can review the section choices against my notes. Processing runs on this PC. This demonstration uses the CPU.”

If generation takes longer than the recording budget, use a clearly labeled cut: **Generation wait shortened in recording**. Do not imply real-time performance that was not measured.

## 1:25–1:55 — Check the reasoning

**Action:** Show the next step and decision section. Click a source link, inspect the quote, and close the source viewer.

**Say:** “The next step is to measure Approach B's memory use. The rejected option remains visible so I do not repeat it. Each statement points to source evidence. Quote matching checks where the wording came from; I still review whether the interpretation is right.”

## 1:55–2:15 — Resume later

**Action:** Save, close the app, reopen it, and select the saved task.

**Say:** “The handoff survives a restart. Notes and attachments stay in storage protected for my Windows account. When I return, I can inspect the evidence and continue with a specific next action.”

## 2:15–2:40 — Snapdragon path

**Screen:** Deployment slide.

**Say:** “The target is Snapdragon-powered HP PCs. Qualcomm AI Hub profiled the Whisper encoder and decoder components on a Snapdragon X Elite reference device's NPU. Full transcription in ThreadBack still uses the CPU, and the Windows application has not yet been tested on Snapdragon or HP hardware.”

## 2:40–2:50 — Close

**Say:** “ThreadBack preserves the reasoning needed to resume a task, with evidence the user can check.”

## Update after hardware validation

After device validation, state the exact device and backend. Add a measured latency only from the matching benchmark report. Do not claim NPU transcription in ThreadBack or energy savings from component profiles, CPU tests, or an ARM64 build.
