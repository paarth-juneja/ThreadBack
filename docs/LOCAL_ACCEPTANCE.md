# Local acceptance and CPU demo check

Use `ThreadBack.cmd` on the development PC. Allow about 15–20 minutes. The built-in example is for a repeatable recording; use a real, non-sensitive study task for the separate personal acceptance pass.

## Personal handoff

1. Create a task with a short goal. Add one note that states a decision, one unresolved question, and a specific next action. Add a PNG/JPEG screenshot if it helps; review and correct its extracted text.
2. Import a short WAV/M4A memo or click **Record**, speak a short handoff, then stop. Review and correct the transcript before generation. The repeatable import fixture is `artifacts/verification/voice-fixture.wav`.
3. Click **Create my resume capsule**. Check each section against the notes. Open at least one source link. Edit one statement and move one statement to a different section; save.
4. Close ThreadBack, launch it again, and select the saved task. Confirm the source, edits, and category move persisted.
5. Export the handoff to Markdown and inspect the file. It is unencrypted and does not include screenshot images. Delete the task in ThreadBack and confirm it disappears from the task list; the exported copy remains separate.

Record the device, date, pass/fail for each action, approximate generation wait, and any exact error text in `WORKING_NOTES.md`. Do not include private study material or microphone audio in shared evidence without reviewing it first.

## Repeatable recording example

Choose **Try an example**. The September 23 local CPU run placed the online-approach rejection under decisions, the unmeasured RAM and final choice under open questions, and the 100-document RAM measurement under next steps. Its output is `artifacts/verification/demo-acceptance-2026-09-23.json`. Generation after model loading took 15.4 seconds on the Intel development laptop; a cold run may take longer. The current narration is in `docs/DEMO_SCRIPT.md`.

This example has two text notes. OCR and synthetic WAV transcription are separate reproducible checks; the live microphone and the personal handoff require participant input.
