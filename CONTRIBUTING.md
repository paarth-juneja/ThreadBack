# Contributing to ThreadBack

ThreadBack uses C#/.NET 10 with WPF and Windows Forms integration. Develop on Windows 10 version 2004 or later, or Windows 11. `global.json` pins SDK 10.0.401 with patch roll-forward.

## Prepare and build

Run from the repository root in PowerShell. For build tools only, without models:

```powershell
.\scripts\Setup.ps1 -Component Sdk
.\scripts\Run.ps1 -Rebuild -BuildOnly
.\scripts\Test.ps1
```

If script execution is restricted, invoke each script with `powershell -NoProfile -ExecutionPolicy Bypass -File <script-path>` and the same arguments. No system-wide execution policy change is needed.

For the complete app and models, run `Install-ThreadBack.cmd`, or:

```powershell
.\scripts\Install-ThreadBack.ps1 -NoLaunch
.\scripts\Run.ps1
```

The launcher uses `.tools/dotnet/dotnet.exe`, sets `THREADBACK_ROOT`, and starts the Release build. Libraries restore during the build. A local development signing certificate is optional; signing runs only if the certificate and tools are already available.

## Making changes

- `src/ThreadBack.App/`: interface, OCR/audio input, screen capture, and settings.
- `src/ThreadBack.Core/`: capsule data, storage, generation, and source checks.
- `tests/ThreadBack.Checks/`: executable checks; `tests/fixtures/`: synthetic scenarios.
- `scripts/`: setup, build, and packaging. Keep runtime/model versions and checksums pinned.

Choose appropriate checks from [docs/TESTING.md](docs/TESTING.md). Explain the problem, resulting behavior, validation, and remaining limits in a pull request. Use synthetic inputs for shared tests and screenshots. Never commit personal tasks, captures, recordings, models, downloaded tools, API keys, signing certificates, or logs.

Keep source validation, cancellation, encrypted storage, and explicit screen/microphone activation intact. Describe hardware support only as far as it has been checked. A model profile or cross-build does not prove that the application works on a target device.

## Packaging

```powershell
.\scripts\Package.ps1 -Architecture x64
.\scripts\Package.ps1 -Architecture arm64 -SelfContained
```

Outputs go to ignored `artifacts/`. Packaging downloads Windows tools and creates a development certificate/package. `-Install` requests installation and machine-level certificate trust; use it only when you intend those changes. Self-contained packages include .NET; models and AI workers need separate setup. Packaging is not required for ordinary development checks.

## Licensing

Review [THIRD_PARTY.md](THIRD_PARTY.md) before distributing models or binaries. The repository currently has no application-code license granting general reuse; the owner must choose one before presenting it as generally reusable open source. Contributor instructions do not grant additional license rights.
