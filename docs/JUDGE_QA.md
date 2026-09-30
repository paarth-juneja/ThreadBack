# ThreadBack: rehearsal answers

**What does it do?**

It turns a deliberate handoff note, screenshot, or short voice memo into a task-resumption brief: the next action, decisions, the last known state, and unresolved questions. Each statement has source evidence that I can inspect.

**Why not just save a note or use a chatbot?**

A note is a useful baseline. ThreadBack adds a consistent handoff structure, explicit uncertainty, source inspection, and persistent task capsules in one local workflow. Whether that improves resumption time needs a user study; I am not claiming a measured improvement yet.

**How is this different from Recall?**

Recall is a relevant adjacent product for retrieving previous activity. My focus is a handoff I deliberately create before stopping: why I rejected an option, what I have not tested, and what I should do next. I am not claiming that task memory is an entirely new category.

**Can the AI make a mistake?**

Yes. A quotation existing in a source does not mean the AI interpreted it correctly. The app checks source references and exact quotations, then lets the user inspect and edit the interpretation. Negation, incomplete experiments, conditional decisions, and instructions embedded in notes are part of the synthetic evaluation.

**Where is the AI running?**

In the current development demonstration, the language model and Whisper transcription run on the Intel laptop's CPU. The intended Snapdragon optimization starts with Whisper on the NPU using Qualcomm's Windows example. I will name the exact hardware and execution provider for any target-device measurements actually completed.

**Why build for Snapdragon-powered HP PCs?**

The intended application is an everyday student workflow on a Windows PC. Local AI is a good fit for personal task material. Snapdragon's NPU is the planned acceleration target for speech, while the Windows ARM64 build prepares the application for that platform. NPU performance and power benefits still require measurement.

**What is stored and how is it protected?**

Only the evidence I choose and the resulting capsule are saved. Windows DPAPI protects the stored payload for my Windows account, including its images and audio. There is no continuous recording. An exported Markdown file is a separate unencrypted copy. This protection does not defend against another program already running as my account.

**What would you improve next?**

First, target-device validation and speech acceleration; then semantic-quality evaluation with more realistic handoffs and a small resumption study. I would compare ordinary notes against ThreadBack and measure correct next-action identification, unsupported statements, and time to resume. I would only add automatic capture if users needed it and could clearly control it.

**Which parts are yours?**

The application workflow and integration were developed for this project with AI assistance. The models, runtimes, and libraries are third-party components and are credited in THIRD_PARTY.md. I should describe my own contribution accurately and check the challenge's current requirements before submitting.

**What is the principal limitation today?**

The app is a prototype, and model interpretation still needs review. Snapdragon/HP validation, NPU measurements, installed-package acceptance, and a user study must be described according to their actual completion status at submission time.
