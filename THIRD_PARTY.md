# Third-party components

| Component | Purpose | Upstream license/source |
|---|---|---|
| .NET 10 / WPF | Windows application | MIT, https://github.com/dotnet/wpf and https://github.com/dotnet/runtime |
| NAudio 2.2.1 | WAV processing and microphone recording | MIT, https://github.com/naudio/NAudio |
| llama.cpp b10964 | Local CPU, Vulkan GPU and OpenVINO NPU model runtimes | MIT, https://github.com/ggml-org/llama.cpp/tree/b10964 |
| Qwen3 4B Q4_K_M | Preferred capsule model | Apache-2.0, https://huggingface.co/Qwen/Qwen3-4B-GGUF |
| Qwen2.5 1.5B Instruct Q4_K_M | Initial development fallback | Apache-2.0, https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF |
| Qwen2.5-VL 3B Q4_K_M and Q8 projector | Optional local screenshot description | Qwen Research License (non-commercial research/evaluation; commercial use requires a separate license), https://huggingface.co/Qwen/Qwen2.5-VL-3B-Instruct/blob/main/LICENSE ; quantized files: https://huggingface.co/ggml-org/Qwen2.5-VL-3B-Instruct-GGUF |
| whisper.cpp b5130 | Local speech runtime | MIT, https://github.com/ggml-org/whisper.cpp/tree/b5130 |
| Whisper Base English | Speech recognition model | MIT, https://github.com/openai/whisper and https://huggingface.co/ggerganov/whisper.cpp |
| Windows OCR / SDK | Screenshot text extraction and packaging | Microsoft platform terms, https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr |

Runtime archives retain their upstream license files. Before redistributing binaries or models beyond this development workspace, include the relevant upstream license/notice texts in the distribution. Setup scripts download assets directly rather than claiming ownership over them.

Pinned model revisions and hashes are in `scripts/Setup.ps1` and `scripts/Setup-Voice.ps1`.
