# Real Comsys Lab viva acceptance: stopped September 27, 2026

> Historical failure report. The participant explicitly authorized a resumed check on September 28. That check succeeded with the development-signed local runtime; see `WORKING_NOTES.md` and `artifacts/verification/comsys-full-vision-2026-09-28.json`. The old five-failure stop was honored until that authorization. The remaining limitation is semantic: screenshots show visible state, while attempted actions and code problems need participant-written context.

## Goal and current state

The participant asked for an end-to-end check of their saved `Comsys Lab viva` capsule: use local vision to understand its screenshots, generate a useful chronological handoff with correctly separated categories, and leave factual/interface judgment to the participant. This check did **not** finish. The real capsule contains one note and seven Eye screenshots. No new timeline, category judgment, or screenshot descriptions were saved to it.

The earlier September 26 single-image check successfully described the final `chan.m` screenshot. The September 27 batch check failed before producing any new description. The model and projector remain present under `models/vision/`; their previously verified expected sizes were 1,929,901,056 and 844,757,728 bytes respectively. No indication of a model-file checksum problem was found in this run.

## Exact failure count and evidence

Five consecutive `LocalVision.DescribeAsync` attempts failed with `IOException`, corresponding to the first five Eye screenshots (runner labels `VISION 2/11` through `VISION 6/11`, because evidence index 1 is the first image). The check was stopped at the fifth failure per `AGENTS.md`. Do not retry the same vision/runtime acceptance step, including slight launch variants, without an explicit new participant request.

The runner logged exception types but not the individual exception messages. Its final stderr was `System.InvalidOperationException: Five vision descriptions failed; stopping this check.` at `tests/ThreadBack.Checks/Program.cs:180`. Windows `Microsoft-Windows-CodeIntegrity/Operational` events 3033 and 3077 at approximately 01:25:17–01:25:20 local time show `llama-server.exe` attempting to load `.tools/llama/ggml.dll`; the DLL did not meet the machine's Enterprise signing-level requirement under policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`. Read-only Authenticode inspection reported `ggml.dll` as `NotSigned`. The precise relationship between this policy rejection and the earlier successful standalone vision run remains unresolved, but the event evidence strongly identifies the startup failure in this run. No policy bypass or settings change was attempted.

Raw non-secret runner output is in `artifacts/verification/real-comsys-run-2026-09-27.stdout.txt` and `.stderr.txt`. The intended `artifacts/verification/real-comsys-handoff-2026-09-27.json` was **not** created. The check process (PID 18256) and any local model server were no longer running after the stop. No Qualcomm job was submitted.

## Changes and commands completed

- `src/ThreadBack.Core/GenerationPassages.cs`: raised the bounded screen-passage limit from six to eight so all seven real Comsys screenshots can be represented. Other limits remain 24 passages and 4,200 selected characters.
- `tests/ThreadBack.Checks/Program.cs`: added a seven-screen regression and a read-only `--real-capsule-check` evaluator. The evaluator loads the participant capsule in memory, would add descriptions only to that in-memory copy, and writes a summary without raw image/OCR bytes only on success.
- Built the test project with `.tools/dotnet/dotnet.exe build tests/ThreadBack.Checks/ThreadBack.Checks.csproj -c Release --nologo -p:NuGetAudit=false`. Passed with zero warnings/errors. Ran it through a hidden `Start-Process` child; 17 checks passed, including the new seven-screen regression. Evidence: `artifacts/verification/real-comsys-core-checks-2026-09-27.txt`.
- Started the same child-process route with `--real-capsule-check 55792561f77c42d69997f36bdceb0c05 --root C:\Stuff\AI\snapdragon --output artifacts/verification/real-comsys-handoff-2026-09-27.json`, redirecting stdout/stderr to the evidence files above. Five vision failures followed. Stopped the isolated check; no successful output report exists.
- Built `src/ThreadBack.App/ThreadBack.App.csproj` in Release successfully; one existing WFO0003 WPF/WinForms DPI analyzer warning remained. Development-signed `ThreadBack.Core.dll` and `ThreadBack.dll` with the existing local certificate. Did not launch the app or regenerate the MSIX package.
- Read-only hash check of the encrypted original capsule before and after: `2e5dc65623a56808784767c9b957db56d4c8619d1a3590870e416859c1b96c09` both times. No participant data was edited. No Windows security settings were changed.

## Next gate

Only after explicit participant authorization, diagnose why the local llama runtime's `ggml.dll` is rejected in this execution context without weakening Windows security. Obtain the actual startup/exception message and compare the signed/approved process path with the failed one. If a compliant runtime path is found, run one bounded visual smoke check, then the real seven-screen handoff, and ask the participant to judge intent and UI. Do not claim that screenshots alone prove attempted downloads, broken code, decisions, or completed work; user-written context is needed for those claims. The September 30 submission deadline means remaining acceptance/submission work should be prioritized independently of this blocked enhancement.
