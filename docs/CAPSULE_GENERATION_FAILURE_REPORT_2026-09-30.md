# Capsule generation failure report — 2026-09-30

## Goal and stop condition

The participant reported that **Create my resume capsule** stopped with: `Stopped after four minutes. Your evidence is saved. Try fewer screenshots or a short progress note.` The goal was to make capsule generation complete for the saved, screen-heavy task without changing its encrypted evidence. The project `AGENTS.md` requires stopping a troubleshooting step after its fifth distinct failed attempt. This step has reached **five failures**, counting the participant's shown run and four supervised checks below. Do not retry capsule generation for this issue until the participant explicitly asks.

## Attempts and results

1. **Participant run, shown in the supplied screenshot:** The app stopped at its original four-minute deadline. The screenshot does not reveal whether the model was drafting its first response or revising an invalid one.
2. **Read-only saved-task text check, original strict schema with a ten-minute deadline:** `dotnet run --project tests/ThreadBack.Checks -c Release --no-restore -- --saved-text-check 55792561-f77c-42d6-9997-f36bdceb0c05`. The 19 core checks passed. The generation request remained active until ten minutes, then threw `System.Threading.Tasks.TaskCanceledException` in `LocalGenerator.GenerateAsync` at the HTTP `PostAsJsonAsync` call (line 110). Full log: `artifacts/verification/saved-text-check-2026-09-30.txt`.
3. **Small synthetic check using `json_object` and a 1,600-token response limit:** `dotnet run --project tests/ThreadBack.Checks -c Release --no-restore -- --goal-context-model`. The 19 core checks passed, but both model responses failed citation/shape validation. Final error: `System.IO.InvalidDataException: The model could not produce a valid evidence-linked capsule.` Log: `artifacts/verification/capsule-json-mode-synthetic-2026-09-30.txt`.
4. **Small synthetic check using a strict schema without the per-source ID enum:** The model produced a structurally valid capsule in about 134 seconds, but the acceptance check failed with `System.Exception: Invented a decision`; it classified fixing an error as a choice. Command: the same `--goal-context-model` check. Log: `artifacts/verification/capsule-light-schema-synthetic-2026-09-30.txt`. Synthetic output: `artifacts/verification/goal-context-ocr-model-2026-09-28.json` (the runner's existing output name).
5. **Read-only saved-task text check with compact OCR context, strict schema without the source-ID enum, and clearer decision wording:** The 19 core checks passed, but the HTTP generation request again remained active until the ten-minute deadline and threw `System.Threading.Tasks.TaskCanceledException` at `LocalGenerator.GenerateAsync` line 110. Log: `artifacts/verification/saved-text-check-compact-2026-09-30.txt`. No capsule was produced.

No Qualcomm AI Hub or Device Cloud job was run; there are no job IDs for this work. No API key was read or copied into the repository.

## Changes made

- `src/ThreadBack.App/MainWindow.xaml.cs`: increased the UI deadline from four to ten minutes and updated the timeout text.
- `src/ThreadBack.Core/LocalGenerator.cs`: increased the HTTP timeout to twelve minutes so the UI cancellation remains authoritative; added generation and revision progress messages; retained strict JSON schema after the `json_object` check failed.
- `src/ThreadBack.Core/HandoffSynthesis.cs`: removed the source-ID enum from the response grammar and tightened the decision instruction. `CapsuleRules.ParseBrief` still checks every returned ID and exact supporting quote against the selected sources.
- `src/ThreadBack.Core/ActivityContext.cs`: limited the text-model projection to 6,000 characters for screen-heavy tasks. The encrypted original evidence is unchanged.
- `tests/ThreadBack.Checks/Program.cs`: added `--saved-text-check` for read-only, content-free timing and count output.

The normal Release app build passed after the final edits with the existing NU1510 and WFO0003 warnings. The 19 core checks passed. Logs: `artifacts/verification/capsule-compact-build-2026-09-30.txt` and `capsule-compact-core-2026-09-30.txt`. Two earlier app-build invocations failed for build-environment reasons: the running app locked `ThreadBack.Core.dll`, then an isolated build gave the app and core the same intermediate directory and produced `NETSDK1005`. A later isolated build and the final normal build passed. These are separate build failures, not generation attempts.

## Confirmed state and unknowns

- The saved task has 11 source items and passes input validation. The active generation setting was CPU, no memory cap, and immediate model unload after work. The installed text model is Qwen3 4B Q4_K_M.
- The model process ran during the real checks and consumed several gigabytes of memory and CPU. The real-task HTTP request did not complete within ten minutes. The exact time spent in prompt processing versus output generation, and whether the model reached a validation retry, is unknown because the server was started with logging disabled and the check did not record stage timing.
- The encrypted saved-task file was not written by the checks. Its post-check SHA-256 was `C45FEE63B336F21FE59064A46C10DA7ECDA6DA45B801CDDAE77C7171AF5802D0` at `%LOCALAPPDATA%/ThreadBack/Capsules/55792561f77c42d69997f36bdceb0c05.tbc`. No real generated brief was saved.
- The previously open ThreadBack process (PID 36332) was stopped to release the build lock. The final Release binaries were built but have not been development-signed or relaunched after the stop. No ThreadBack or llama-server process remained in the final process check. The current source changes are unverified for successful generation on this saved task.

## Next investigation

See `docs/CAPSULE_GENERATION_ESCALATION_PROMPT_2026-09-30.md`. A deeper investigation should measure model prefill and decoding separately without logging participant content, then design a bounded generation path and verify the complete saved task in memory. Respect the five-failure stop until the participant explicitly resumes this step.

## Subsequent user-requested edit

The participant later requested prompt and context edits, with a separate check-in before another capsule run. The image and capsule prompts now identify unrelated activity without treating it as goal progress or using concrete examples. Described screens omit duplicate OCR from the final text-model request; OCR-only screens send a contiguous excerpt of at most 650 characters. Twenty source checks and the Release build passed, and the app assemblies were development-signed. No model generation or app launch was run for these edits. See the newer `WORKING_NOTES.md` entry and `artifacts/verification/distraction-context-*-2026-09-30.txt`. The prior ten-minute timeouts remain unresolved.
