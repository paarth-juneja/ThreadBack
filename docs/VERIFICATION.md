# ThreadBack verification — September 22, 2026

The local prototype works on the development laptop. It is not yet validated on Snapdragon or an HP target PC. Raw synthetic results are in `artifacts/verification`; hardware details are in `environment.json`.

## Environment and build

- Intel Core Ultra 5 225U, Windows, approximately 16 GB RAM.
- .NET SDK 10.0.401; framework-dependent desktop application.
- Qwen3 4B Q4_K_M through llama.cpp b10964, CPU. The model download passed the pinned SHA256 check.
- Whisper Base English through whisper.cpp b5130, CPU.
- Final x64 application build: zero warnings and errors. x64 and ARM64 development MSIX packages built and signed successfully.
- ARM64 is a cross-build only. The development MSIX has not passed installed-package validation; installation encountered certificate-trust error 0x800B0109. The direct launcher works on the development computer.

## Functional checks

All 10 checks in `ThreadBack.Checks` passed: protected text/attachment roundtrip, atomic overwrite, invalid citations, missing citations/null sections, invented quotations with real source IDs, user-edit preservation, input limits, duplicate IDs, corrupt-file preservation/reporting, and deletion.

The real-model smoke test also passed exact source-wording preservation, pre-cancelled requests, and cancellation during inference. Cancellation stops the owned model worker. The application's smoke run generated the sample, saved its protected payload, reloaded it, rendered the resume view, and exited. This checks save/reload within the application run; a user close/relaunch acceptance pass is still desirable.

Screenshot OCR produced text in the local smoke run. The transcript of a synthetic WAV preserved the rejected approach and pending memory measurement. That single transcription took 23.03 seconds including the speech worker/model load. It is not a word-error-rate test, live microphone test, or Snapdragon benchmark. OCR recognition errors are possible and the extracted text remains editable. Microsoft documents package identity for supported desktop OCR deployment even though this unpackaged local smoke test succeeded.

## Model evaluation

The final approach asks the model to classify source passages, then copies their wording directly. It displays at most three passages per section. Original evidence is retained. Users can edit wording and use **Move…** to change a section. The prototype does not claim semantic verification from an exact quotation match.

All 12 synthetic cases returned structurally valid, source-linked capsules. All explicitly required decision keywords were present in the decision sections. Eleven of twelve unknown-keyword checks passed. The remaining flag is the circuits example: “I have not solved problem 3” appeared under last state, while the formula doubt remained under open questions. The incomplete work was preserved, but not in the expected category.

Manual review also found category issues that the keyword checks did not catch:

| Case | Observation |
|---|---|
| Offline search | Rejection, missing memory measurement, lack of a final choice, and next benchmark preserved. |
| Login bug | Ruled-out cache, failed refresh observation, untested endpoint, and next capture preserved. |
| Circuits revision | No invented decision; incomplete problem appears in last state rather than unknowns. |
| Dataset selection | Rejected license and unchecked alternative preserved; the instruction not to train yet appears under last state. |
| Missing timing results | No result invented; the written-but-unexecuted benchmark is categorized as a next action rather than an unknown/state. |
| Conflicting memory measurements | Both reported numbers and the warning that neither is validated remain visible. |
| Untrusted README instruction | The injected success instruction is omitted; the unrun experiment and next benchmark are preserved. This is one adversarial example, not proof of general injection resistance. |
| Conditional database plan | SQLite choice, untested multi-user access, and conditional contention test preserved. |
| Report draft | Completed methods, rejected graph, unfinished corrected graph, and next regeneration preserved. |
| Algorithm revision | Solved recursion, missing memoization, unclear subproblems, and next call tree preserved. |
| Failed audio pipeline | Failure and unmeasured accuracy preserved; failure is categorized as an unknown rather than observed state. |
| Camera placement | Chosen overhead position, daylight-only observation, unknown low-light behavior, and next test preserved. |

These are small development fixtures. Some filenames contain `heldout`, but all fixtures have been viewed during iterative development. They are not a fresh blinded test set. Neither keyword matches nor structural validity should be presented as an accuracy percentage.

The final 12-case run recorded 4.82–16.67 seconds per inference after model loading, median 8.94 seconds. Other development activity occurred during the run. The language-model process peaked near 5.24 GiB working set. A separate full-app demo run recorded about 24.1 seconds after loading. These are diagnostic observations, not controlled comparative benchmarks; cold startup and end-to-end user latency are not summarized by those numbers.

## Presentation and remaining acceptance work

The presentation uses actual prototype screenshots and separates the local CPU demonstration from the intended Snapdragon optimization. Sources are in speaker notes. Its package/layout/font checks and reimport are recorded privately in `artifacts/deck-build/validation-final.json`; visual review is performed on the rendered slides. Native PowerPoint playback is not part of those automated checks.

Still required for a target-device result: exact cloud-device identification, Windows runtime/worker deployment, CPU reference measurement, validated Qualcomm speech backend/provider, and application acceptance on that device. Only an actual HP test establishes HP-specific compatibility. NPU latency and energy savings remain unmeasured.

Also pending: live microphone and interactive editing/moving acceptance, independent network-isolation testing, installed-package validation, broader semantic evaluation, and a user study. The prototype and proposal can be demonstrated with these limitations disclosed; they are not a production-readiness certification.
