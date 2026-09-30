"""Resume Qualcomm's public Whisper archive with verified byte ranges."""

import concurrent.futures
import os
import re
import sys
import time
import zipfile
from pathlib import Path

import requests


URL = "https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/whisper_base/releases/v0.62.2/whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite.zip"
SIZE = 180_777_567
ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / ".tools" / "qualcomm-whisper" / "whisper-base-x-elite.zip"
PARTS = DEST.parent / "range-parts"
CHUNK = 2 * 1024 * 1024


def get_range(index, start, end):
    target = PARTS / f"{index:04d}.part"
    length = end - start + 1
    if target.exists() and target.stat().st_size == length:
        return index
    for attempt in range(4):
        try:
            response = requests.get(URL, headers={"Range": f"bytes={start}-{end}"}, timeout=(25, 180))
            match = re.fullmatch(r"bytes (\d+)-(\d+)/(\d+)", response.headers.get("Content-Range", ""))
            if response.status_code != 206 or not match or tuple(map(int, match.groups())) != (start, end, SIZE):
                raise RuntimeError(f"Unexpected range response: {response.status_code}")
            if len(response.content) != length:
                raise RuntimeError("Incomplete range response")
            temporary = target.with_suffix(".tmp")
            temporary.write_bytes(response.content)
            temporary.replace(target)
            return index
        except (requests.RequestException, RuntimeError):
            if attempt == 3:
                raise
            time.sleep(2 * (attempt + 1))


def main():
    DEST.parent.mkdir(parents=True, exist_ok=True)
    PARTS.mkdir(exist_ok=True)
    prefix = DEST.stat().st_size if DEST.exists() else 0
    if prefix > SIZE:
        raise RuntimeError("Existing archive is larger than the expected source.")
    if prefix == SIZE:
        print("Archive already complete.")
    else:
        ranges = [(i, start, min(start + CHUNK, SIZE) - 1) for i, start in enumerate(range(prefix, SIZE, CHUNK))]
        print(f"Resuming at {prefix:,}/{SIZE:,} bytes in {len(ranges)} ranges.", flush=True)
        with concurrent.futures.ThreadPoolExecutor(max_workers=24) as pool:
            futures = [pool.submit(get_range, *item) for item in ranges]
            for done, future in enumerate(concurrent.futures.as_completed(futures), 1):
                future.result()
                if done % 12 == 0 or done == len(futures):
                    print(f"Downloaded {done}/{len(futures)} ranges.", flush=True)
        with DEST.open("ab") as output:
            for index, start, end in ranges:
                part = PARTS / f"{index:04d}.part"
                if part.stat().st_size != end - start + 1:
                    raise RuntimeError("A range changed size before assembly.")
                with part.open("rb") as source:
                    while data := source.read(1024 * 1024):
                        output.write(data)
        print(f"Assembled {DEST.stat().st_size:,} bytes.", flush=True)
    if DEST.stat().st_size != SIZE:
        raise RuntimeError("Archive size does not match the source.")
    with zipfile.ZipFile(DEST) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError(f"ZIP integrity check failed: {bad}")
        print("ZIP integrity passed; entries:")
        for name in archive.namelist():
            print("  " + name)
    return 0


if __name__ == "__main__":
    sys.exit(main())
