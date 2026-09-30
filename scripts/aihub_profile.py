"""Profile one extracted Whisper QNN ONNX component on Snapdragon X Elite.

The API token is read from stdin. Only non-secret IDs and profiles are saved.
"""

import argparse
import hashlib
import json
import logging
import sys
from datetime import datetime, timezone
from pathlib import Path

import qai_hub as hub


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts" / "verification" / "aihub-whisper"
RECORD = OUTPUT / "jobs.json"
DEVICE = "Snapdragon X Elite CRD"


def now():
    return datetime.now(timezone.utc).isoformat()


def load_record():
    if RECORD.exists():
        return json.loads(RECORD.read_text(encoding="utf-8"))
    return {"device": DEVICE, "components": {}}


def save_record(record):
    OUTPUT.mkdir(parents=True, exist_ok=True)
    temporary = RECORD.with_suffix(".tmp")
    temporary.write_text(json.dumps(record, indent=2), encoding="utf-8")
    temporary.replace(RECORD)


def fingerprint(path):
    digest = hashlib.sha256()
    if path.is_file() and path.suffix.lower() == ".bin":
        files = [path]
    elif path.is_dir() and path.suffix.lower() == ".onnx":
        files = sorted(p for p in path.rglob("*") if p.is_file())
        if not any(p.suffix == ".onnx" for p in files) or not any(p.suffix == ".bin" for p in files):
            raise ValueError("ONNX model directory needs an ONNX file and its QNN binary.")
    else:
        raise ValueError("Pass a precompiled .onnx model directory or QNN .bin file.")
    for file in files:
        digest.update((file.name if path.is_file() else file.relative_to(path).as_posix()).encode("utf-8"))
        with file.open("rb") as source:
            for block in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(block)
    return digest.hexdigest()


def find_existing_job(client, name):
    for summary in client.get_job_summaries(limit=100):
        if summary.name == name and summary.device_name == DEVICE:
            return summary.job_id
    return None


def update_status(client, record):
    changed = False
    if not record["components"]:
        matches = [s for s in client.get_job_summaries(limit=100) if s.name.startswith("ThreadBack Whisper-Base ")]
        if matches:
            for summary in matches:
                print(f"Existing Workbench job: {summary.job_id} {summary.name} {summary.status.code}")
        else:
            print("No ThreadBack Whisper profile jobs found in the 100 most recent account jobs.")
    for component, entry in record["components"].items():
        job_id = entry.get("jobId")
        if not job_id:
            print(f"{component}: model {entry.get('modelId', 'pending')}; no job ID yet")
            continue
        job = client.get_job(job_id)
        status = job.get_status()
        entry["status"] = status.code
        entry["checkedAt"] = now()
        changed = True
        print(f"{component}: {job_id} {status.code} {job.url}")
        if status.code == "FAILED":
            entry["statusMessage"] = status.message
            if "logPaths" not in entry:
                logs = job.download_job_logs(str(OUTPUT / f"{component}-{job_id}-logs"))
                entry["logPaths"] = [str(Path(log).resolve().relative_to(ROOT)) for log in logs]
            print(f"  Failure: {status.message or 'see saved logs'}")
            print(f"  Logs saved: {len(entry['logPaths'])}")
        if status.success and not entry.get("profilePath"):
            destination = OUTPUT / f"{component}-{job_id}-profile.json"
            job.download_profile(str(destination))
            entry["profilePath"] = str(destination.relative_to(ROOT))
            changed = True
            print(f"  Raw profile: {destination}")
    if changed:
        save_record(record)


def submit(client, record, model_path, component, options=None):
    model_path = model_path.resolve()
    digest = fingerprint(model_path)
    binary = model_path.is_file()
    name = f"ThreadBack Whisper-Base {component} {'bin ' if binary else ''}{digest[:12]}"
    entry = record["components"].get(component)
    if entry and entry.get("sha256") != digest:
        if entry.get("status") != "FAILED":
            raise ValueError("A different model is already recorded for this component; inspect jobs.json first.")
        record.setdefault("history", []).append({"component": component, **entry})
        del record["components"][component]
        save_record(record)
        entry = None
    if entry and entry.get("jobId"):
        print(f"Already submitted {component}: {entry['jobId']}")
        return
    existing = find_existing_job(client, name)
    if existing:
        entry = entry or {}
        entry.update({"sha256": digest, "name": name, "jobId": existing, "recoveredAt": now()})
        record["components"][component] = entry
        save_record(record)
        print(f"Found existing job for {component}: {existing}")
        return
    if entry and entry.get("submissionIntentAt"):
        raise ValueError("A previous submission has an uncertain result. Check the Workbench jobs page before another submission.")
    entry = entry or {"sha256": digest, "name": name, "modelPath": str(model_path.relative_to(ROOT))}
    entry["options"] = options if options is not None else ("--qairt_version 2.45 --compute_unit npu" if binary else "")
    record["components"][component] = entry
    save_record(record)
    if not entry.get("modelId"):
        uploaded = client.upload_model(str(model_path), name=name)
        entry["modelId"] = uploaded.model_id
        entry["uploadedAt"] = now()
        save_record(record)
        print(f"Uploaded {component} model: {uploaded.model_id}")
    # Record intent before the remote call. If the response is lost, search jobs by name.
    entry["submissionIntentAt"] = now()
    save_record(record)
    job = client.submit_profile_job(
        model=client.get_model(entry["modelId"]),
        device=hub.Device(DEVICE),
        name=name,
        options=entry["options"],
        retry=False,
    )
    entry["jobId"] = job.job_id
    entry["submittedAt"] = now()
    save_record(record)
    print(f"Submitted {component}: {job.job_id} {job.url}")


def retry_debug(client, record, component):
    entry = record["components"].get(component)
    if not entry or entry.get("status") != "FAILED" or not entry.get("modelId"):
        raise ValueError("A recorded failed profile with a model ID is required for debug retry.")
    name = f"ThreadBack Whisper-Base {component} debug {entry['sha256'][:12]}"
    existing = find_existing_job(client, name)
    record.setdefault("history", []).append({"component": component, **entry})
    replacement = {key: entry[key] for key in ("sha256", "modelPath", "modelId") if key in entry}
    replacement["name"] = name
    replacement["options"] = "--runtime_debug=true --max_profiler_iterations=1 --onnx_execution_providers=qnn --qairt_version 2.45"
    record["components"][component] = replacement
    if existing:
        replacement["jobId"] = existing
        replacement["recoveredAt"] = now()
        save_record(record)
        print(f"Found existing debug job for {component}: {existing}")
        return
    replacement["submissionIntentAt"] = now()
    save_record(record)
    job = client.submit_profile_job(
        model=client.get_model(replacement["modelId"]),
        device=hub.Device(DEVICE),
        name=name,
        options=replacement["options"],
        retry=False,
    )
    replacement["jobId"] = job.job_id
    replacement["submittedAt"] = now()
    save_record(record)
    print(f"Submitted debug profile for {component}: {job.job_id} {job.url}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("submit", "status", "frameworks", "retry-debug", "submit-repaired-encoder", "submit-decoder-binary"))
    parser.add_argument("--model", type=Path)
    parser.add_argument("--component", choices=("encoder", "decoder"))
    args = parser.parse_args()
    if args.action == "submit" and (args.model is None or args.component is None):
        parser.error("submit requires --model and --component")
    if args.action == "retry-debug" and args.component is None:
        parser.error("retry-debug requires --component")
    logging.disable(logging.CRITICAL)
    token = sys.stdin.readline().strip()
    if not token:
        print("No saved API token received.", file=sys.stderr)
        return 1
    try:
        client = hub.Client(hub.ClientConfig(api_token=token))
        token = None
        record = load_record()
        if args.action == "submit-decoder-binary":
            submit(client, record,
                   ROOT / '.tools/qualcomm-whisper/prepared/decoder.onnx/decoder.bin',
                   'decoder-binary',
                   '--qairt_version 2.45 --compute_unit npu --max_profiler_iterations=1')
        elif args.action == "submit-repaired-encoder":
            submit(client, record,
                   ROOT / '.tools/qualcomm-whisper/prepared-validated/encoder.onnx',
                   'encoder-onnx-repaired',
                   '--runtime_debug=true --max_profiler_iterations=1 --onnx_execution_providers=qnn --qairt_version 2.45')
        elif args.action == "submit":
            submit(client, record, args.model, args.component)
        elif args.action == "retry-debug":
            retry_debug(client, record, args.component)
        elif args.action == "frameworks":
            for framework in client.get_frameworks():
                print(framework.name, framework.api_version, framework.full_version, ",".join(framework.api_tags))
        else:
            update_status(client, record)
        return 0
    except (ValueError, FileNotFoundError) as error:
        print(str(error), file=sys.stderr)
    except Exception as error:
        # API failures can contain request details, so never echo the exception.
        print(f"AI Hub request failed ({type(error).__name__}). Check jobs.json and the Workbench jobs page before retrying.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
