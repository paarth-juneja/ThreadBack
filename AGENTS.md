# Project instructions

## GitHub publication

Do not push commits to a remote unless the user explicitly asks for a push. Creating or changing files does not authorize publication.

## Failure limit

For any single implementation or troubleshooting step, count distinct failed attempts across sessions. If more than four attempts fail (the fifth failure), stop retrying that step. Do not switch to slightly different variants of the same attempt to reset the count.

At that point, give the user a detailed report covering the goal, exact attempt count, changes made, commands or jobs run, error messages, evidence paths, confirmed facts, unresolved causes, and current project state. Also provide a self-contained prompt the user can paste into a more capable AI model to diagnose and fix the issue. Save the report and prompt in the project for future sessions. Resume the step only when the user explicitly asks.

Keep the participant's Qualcomm API key private and outside this repository. Record job IDs and raw non-secret results in `artifacts/verification` and concise progress in `WORKING_NOTES.md`. Do not regenerate submission slides, proposal, or reports after each intermediate step.
