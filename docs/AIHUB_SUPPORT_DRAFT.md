# Draft for Qualcomm AI Hub support — not sent

To: ai-hub-support@qti.qualcomm.com

Official contact verified September 23, 2026: https://workbench.aihub.qualcomm.com/docs/contact.html

Subject: Whisper-Base X Elite: both direct QNN binaries succeed; precompiled ONNX profiles return generic device error

Hello Qualcomm AI Hub team,

We are profiling the official Whisper-Base v0.62.2 precompiled QNN ONNX float asset for Snapdragon X Elite on Snapdragon X Elite CRD (Windows 11 ARM64, SC8380XP/Hexagon v73). Archive metadata specifies QAIRT 2.45.0.260326154327 and ONNX Runtime 1.27.1. Client is qai-hub 0.55.0.

Job history:

| Job | Component/path | Result |
|---|---|---|
| jp0m6l9ng | Encoder ONNX, original filenames | ONNX model and QNN context binary file must have same basename |
| j5wl4qrmp | Encoder ONNX, corrected basename/reference | Unexpected device error |
| jgk283kng | Decoder ONNX, corrected basename/reference | Unexpected device error |
| jgzlnrqx5 | Encoder ONNX, debug | Unexpected device error |
| jprlwem7p | Decoder ONNX, debug | Unexpected device error |
| jpe7yk985 | Encoder ONNX with explicit com.microsoft opset 1, same debug settings | Unexpected device error |
| j568wnxyg | Direct encoder QNN binary | Success: 100 runs, median 49.3045 ms, all 556 execution-detail entries NPU |
| j5qlv3kep | Direct decoder QNN binary | Success: one measured run, 5.245 ms, all 975 execution-detail entries NPU |

Both debug jobs used `--runtime_debug=true --max_profiler_iterations=1 --onnx_execution_providers=qnn --qairt_version 2.45`. The successful binary job used `--qairt_version 2.45 --compute_unit npu`. The four downloadable failure logs show only SOC/QNN/FP16 detection, with no underlying exception.

The later successful direct decoder job used `--qairt_version 2.45 --compute_unit npu --max_profiler_iterations=1`, model `mq9yw080n`. Both direct component binaries now execute successfully on the NPU, whereas the ONNX wrappers fail. This strengthens the evidence for a problem in the ONNX/EP/service path but does not identify its root cause. The decoder measurement is only one invocation, not a latency distribution or transcription benchmark.

The original archive uses `<component>_qairt_context.bin`. We renamed it to `<component>.bin` and changed only the EPContext relative reference to match the wrapper basename. Byte comparison confirms both context binaries are unchanged and the wrapper preparation changed nothing else.

Offline inspection found that both original IR-13 wrappers contain `com.microsoft::EPContext` but import only the default opset. ONNX 1.21.0 reports `No opset import for domain 'com.microsoft'`; adding the Microsoft-domain import at version 1 passes structural checking without changing IR/tensors/binaries. The repaired encoder was subsequently uploaded as model `mq26x250n` and profiled once as `jpe7yk985` with identical debug settings; it still failed with the same generic error and two-line capability log. The repaired decoder has not been tested. We understand ORT may add registered domains during loading; the domain repair did not resolve the remote issue.

Could you retrieve the underlying worker stderr/exception for the five generic failures, especially latest job `jpe7yk985`, and confirm:

1. The actual ORT, QNN EP plugin, QAIRT/backend and device-driver versions used, and whether this asset/runtime combination is supported.
2. Whether failure occurs in service validation, ORT session creation, external context resolution, QNN deserialization/graph binding or execution.
3. The extracted wrapper/context filenames and whether the wrapper is loaded by path or from a buffer with `ep.context_file_path` set.
4. Whether the omitted Microsoft-domain import is a known export issue and whether corrected official assets are available.

Source asset: https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/whisper_base/releases/v0.62.2/whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite.zip

Archive SHA-256: `026660d55287f98bd37d53b461bb0604661e0427be6113e691efc9300309c798`.

We have paused all further cloud attempts. Both components have direct QNN execution evidence; ONNX execution and end-to-end transcription remain unverified. Non-secret job records, downloaded runtime logs, successful profiles and the local checker audit are available if needed.

Thank you.
