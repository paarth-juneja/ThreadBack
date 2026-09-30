# ThreadBack: a handoff to your future self

## Problem

Students often pause project work to attend a class, prepare for an exam, or switch assignments. Reopening a document can recover the material without recovering the reasoning: which approach failed, why it was rejected, what remains untested, and where to start next.

ThreadBack addresses this specific interruption problem. Its first audience is students doing technical projects alongside coursework.

## Solution

Before stopping, a user adds a few notes or screenshots and optionally records a short voice handoff. ThreadBack processes those inputs locally and creates a Resume Capsule with four sections: the next step, decisions worth keeping, the last known state, and unresolved questions.

The model assigns source passages to these sections; the application preserves their original wording and links each passage to its evidence. This extractive approach avoids inventing summary wording, although incorrect categorization and omissions remain possible. Up to three passages appear in each section. Users can inspect every source, edit the handoff, and save it. The capsule remains available after the application restarts.

For example, a student comparing offline search approaches can preserve that Approach A was rejected because it requires internet, while Approach B's memory use remains unmeasured. The next session begins with the pending memory test rather than repeating the rejected experiment.

## Differentiation

Activity-retrieval systems already help users find past content. Microsoft Recall is a relevant adjacent product and can reopen content found in snapshots. ThreadBack's proposed distinction is an explicit task handoff that preserves decisions, rejected options, and uncertainty beside source evidence.

The prototype uses deliberate user-selected inputs. It does not require continuous screen capture. Its value hypothesis is faster, more accurate task resumption. That hypothesis still requires a user study; no productivity improvement percentage is claimed.

## Technical implementation

The Windows interface uses WPF and .NET. OCR extracts text from selected screenshots. Whisper transcribes short English voice notes. A quantized local language model organizes the evidence into a structured handoff. The generator uses constrained JSON, source-reference validation, and exact-quote matching. These measures improve inspectability but do not prove semantic correctness. The interface therefore presents the output for user review.

The app encrypts capsule payloads, including attached images and audio, with Windows DPAPI for the current account. Text exports require a user action and produce an unencrypted copy. AI workers run locally, and the language-model endpoint binds to loopback with a per-launch token. The application does not execute instructions found in the captured content.

## Snapdragon-powered HP PC strategy

ThreadBack is intended to be optimized for Snapdragon-powered HP PCs. The first optimization target is speech transcription using Qualcomm's Windows Whisper example and ONNX Runtime's QNN execution provider. The application separates the generation and speech workers so a validated Snapdragon backend can replace the initial CPU worker without redesigning the interface.

The evaluation path is: establish a CPU reference, compile/profile the speech model using AI Hub, test the full application on an available Windows Snapdragon device through Device Cloud, and finally validate a Snapdragon-powered HP PC. Measurements will identify the actual device, model revision, runtime, and execution provider.

Current local development uses an Intel Windows laptop. CPU results must not be presented as Snapdragon measurements. NPU performance, battery savings, and HP compatibility are pending validation.

## Deployment and accessibility

The prototype provides x64 and ARM64 build targets, direct launch instructions, and a signed development MSIX. The development certificate requires explicit trust before installation. Desktop runtime prerequisites are documented. Text input is available as an alternative to voice, and the interface supports keyboard navigation and editable OCR/transcription.

## Evaluation and roadmap

The test suite covers encrypted save/reopen, attachment preservation, invalid source references, input limits, corrupted-file handling, deletion, and cancellation. Twelve synthetic scenarios cover interrupted study, debugging, research comparisons, incomplete experiments, and untrusted input. Exact test results and limitations belong in the accompanying verification report.

Following the competition prototype, planned stages add user-triggered window capture, task checkpoints, changed-file indicators, model optimization, and a larger resumption study. Arduino is unnecessary for this workflow.

## Sources

- Challenge: https://api.unstop.com/competitions/crp-snapdragon-ai-lab-build-present-challenge-qualcomm-1748893
- Microsoft Recall: https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/recall/
- Qualcomm Windows Whisper example: https://aihub.qualcomm.com/apps/whisper_windows_py
- Qualcomm AI Hub: https://aihub.qualcomm.com/
- Qualcomm Device Cloud: https://qdc.qualcomm.com/support/faq

This proposal describes the prototype and its intended optimization path. Adapt field lengths to the actual submission form and include only completed measurements from the current verification report.
