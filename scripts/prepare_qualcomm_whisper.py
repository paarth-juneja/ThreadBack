"""Validate and split the Qualcomm Whisper archive into uploadable components."""

import argparse
import json
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools' / 'onnx-audit'))
import onnx


BASE = ROOT / ".tools" / "qualcomm-whisper"
ARCHIVE = BASE / "whisper-base-x-elite.zip"
SOURCE_DIR = "whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite/"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=BASE / 'prepared-validated')
    args = parser.parse_args()
    if onnx.IR_VERSION < 13:
        raise RuntimeError('Use ONNX >= 1.20 with IR 13 checker support; do not downgrade the model IR.')
    with zipfile.ZipFile(ARCHIVE) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError(f"ZIP integrity failed: {bad}")
        names = set(archive.namelist())
        expected = {SOURCE_DIR + "metadata.json"}
        for component in ("encoder", "decoder"):
            expected.update({SOURCE_DIR + f"{component}.onnx", SOURCE_DIR + f"{component}_qairt_context.bin"})
        if not expected.issubset(names):
            raise RuntimeError(f"Missing required files: {sorted(expected - names)}")
        metadata = json.loads(archive.read(SOURCE_DIR + "metadata.json"))
        for component in ("encoder", "decoder"):
            destination = args.output / f"{component}.onnx"
            destination.mkdir(parents=True, exist_ok=True)
            wrapper = onnx.load_from_string(archive.read(SOURCE_DIR + f"{component}.onnx"))
            expected_reference = f"./{component}_qairt_context.bin".encode()
            replacement = f"./{component}.bin".encode()
            references = [attribute for node in wrapper.graph.node for attribute in node.attribute
                          if attribute.name == "ep_cache_context"]
            if len(references) != 1 or references[0].s != expected_reference:
                raise RuntimeError(f"Unexpected QNN context reference for {component}")
            references[0].s = replacement
            # The official archive uses this custom domain without importing it.
            imports = {entry.domain: entry.version for entry in wrapper.opset_import}
            if 'com.microsoft' not in imports:
                wrapper.opset_import.append(onnx.helper.make_opsetid('com.microsoft', 1))
            elif imports['com.microsoft'] != 1:
                raise RuntimeError('Unexpected com.microsoft opset version')
            onnx.checker.check_model(wrapper)
            onnx.save(wrapper, destination / f"{component}.onnx")
            with archive.open(SOURCE_DIR + f"{component}_qairt_context.bin") as source, (destination / f"{component}.bin").open("wb") as output:
                while block := source.read(1024 * 1024):
                    output.write(block)
            old_name = destination / f"{component}_qairt_context.bin"
            if old_name.exists():
                old_name.unlink()
            print(f"{component}: {destination}")
            for file in destination.iterdir():
                print(f"  {file.name}: {file.stat().st_size:,} bytes")
        print("Metadata keys:", ", ".join(sorted(metadata)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
