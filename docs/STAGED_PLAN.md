# ThreadBack: September 22–30 delivery plan

The goal is a working, evidence-linked Windows task-handoff prototype, a clear Snapdragon optimization path, and a presentation the participant can explain and record. The participant's remaining time budget is 6–10 hours. Dates below are targets; cloud access and measured results determine which hardware claims can be included.

| Stage | Target | Assistant work | Participant time | Exit condition |
|---|---|---|---|---|
| 1. Local prototype | September 22–23 | Implement selected notes/screenshots/voice, local generation, quotations, protected storage, source review; verify representative cases | 30 minutes to try one personal study task | Complete capture → generate → inspect → save → reopen workflow |
| 2. Identify hardware | September 23 | Prepare the device inspection and deployment steps | 30 minutes to check accounts and return device information | Exact Windows Snapdragon device and session allowance known |
| 3. Device validation | September 24–26 | Match ARM64 workers, diagnose returned logs, attempt the official Qualcomm Whisper path, compile a measurement report | 90 minutes across short guided sessions | Full application tested on the named device; NPU execution recorded only if verified |
| 4. Freeze the demo | September 26–27 | Review evidence fidelity, fix consequential failures, finalize example and fallback behavior | 45 minutes to review the workflow and microphone | A repeatable, truthful demonstration with no unresolved critical failure |
| 5. Submission materials | September 27–28 | Update proposal, editable presentation, narration and likely judge questions with actual results | 45 minutes for factual review and participant details | All claims match the implementation and recorded evidence |
| 6. Record and submit | September 28–29 | Assist with rehearsal and recording issues, check final files against the form | 90 minutes for rehearsal/recording and 30 minutes for submission | Participant reviews the final intake fields and submits once |
| Buffer | September 30 | Resolve a last-minute upload or packaging issue | Reserve 60–120 minutes if needed | Submission receipt retained |

Base participant effort: 6 hours. With contingency: 7–8 hours, leaving room within the 10-hour ceiling. Avoid relying on the last day; confirm the exact submission cutoff and timezone in the live form.

## Scope choices

The creative focus is preserving rejected options and unresolved reasoning, supported by evidence, so returning to work starts with an actionable next step. This is a differentiation hypothesis, not a claim that nobody has built a similar product.

Keep the first version to explicit handoffs. Exclude Arduino, continuous screen recording, browser automation, automatic command execution, cross-device synchronization, and training a new language model. Those additions would consume time without strengthening the central demo.

The first Snapdragon acceleration target is speech transcription. The language-model worker remains a clearly labeled CPU baseline until an alternative backend is implemented and measured. An ARM64 build is useful preparation but is not device validation.

## Decision gates

- If Windows Device Cloud access is unavailable by September 24, preserve the local demo and proceed with an explicitly intended Snapdragon optimization proposal. An AI Hub profile, if obtained, must be described as a model result rather than a full-app test.
- If NPU integration is not stable by September 26, freeze a working CPU demonstration on the actual available device. Document the NPU work as pending; never substitute invented performance figures.
- If model wording is wrong, expose and correct it in the workflow. Do not quietly replace the output with a canned capsule while presenting it as a live AI result.
- If the signed development package cannot be installed, use the working direct launcher on the development PC and report installation as pending. Keep Windows OCR's supported packaging requirement visible in technical documentation.

## Learning without a course workload

No course completion is a dependency of this plan. Budget at most 20–30 optional minutes for the concept or demonstration needed to explain the project. The assistant handles the implementation research.

The [Qualcomm Academy catalog](https://academy.qualcomm.com/course-catalog) lists AI Hub and Windows on Snapdragon tracks. Its public page did not expose individual course details during this check, so no course duration or certificate requirement is assumed. The [Windows on Snapdragon developer page](https://www.qualcomm.com/developer/windows-on-snapdragon) and [official Windows Whisper example](https://aihub.qualcomm.com/apps/whisper_windows_py) are the focused engineering references.

## Immediate participant handoff

Return only the listed Windows device model and available session minutes from Qualcomm Device Cloud. Then follow the short inspection step in QUALCOMM_RUNBOOK.md. Do not send account passwords or API tokens. The exact execution instructions will be matched to that device.
