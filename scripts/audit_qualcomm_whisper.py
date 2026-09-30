"""Offline provenance and structural checks. Never imports the AI Hub client."""
import hashlib
import json
import statistics
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Isolated checker; leave the recorded AI Hub client environment unchanged.
sys.path.insert(0, str(ROOT / '.tools' / 'onnx-audit'))
import onnx


def sha(data):
    return hashlib.sha256(data).hexdigest()


def check(model):
    try:
        onnx.checker.check_model(model)
        return 'PASS (structural only; custom EP kernel execution not checked)'
    except onnx.checker.ValidationError as error:
        return str(error)


def main():
    base = ROOT / '.tools' / 'qualcomm-whisper'
    out = ROOT / 'artifacts' / 'verification' / 'aihub-whisper'
    report = {'onnx_version': onnx.__version__, 'checker_ir': onnx.IR_VERSION,
              'cloud_calls': 0, 'components': {}}
    assert onnx.IR_VERSION >= 13
    archive_path = base / 'whisper-base-x-elite.zip'
    report['archive_sha256'] = sha(archive_path.read_bytes())
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None
        prefix = 'whisper_base-precompiled_qnn_onnx-float-qualcomm_snapdragon_x_elite/'
        report['metadata'] = json.loads(archive.read(prefix + 'metadata.json'))
        for component in ('encoder', 'decoder'):
            original = onnx.load_from_string(archive.read(prefix + component + '.onnx'))
            prepared = onnx.load(base / 'prepared' / (component + '.onnx') / (component + '.onnx'))
            repaired = onnx.load(base / 'prepared-validated' / (component + '.onnx') / (component + '.onnx'))
            expected = onnx.ModelProto()
            expected.CopyFrom(original)
            refs = [a for n in expected.graph.node for a in n.attribute if a.name == 'ep_cache_context']
            assert len(refs) == 1
            refs[0].s = ('./' + component + '.bin').encode()
            assert expected == prepared, 'Prepared wrapper changed beyond the filename'
            expected.opset_import.append(onnx.helper.make_opsetid('com.microsoft', 1))
            assert expected == repaired, 'Repair changed beyond filename and domain import'
            original_binary = archive.read(prefix + component + '_qairt_context.bin')
            for folder in ('prepared', 'prepared-validated'):
                assert (base / folder / (component + '.onnx') / (component + '.bin')).read_bytes() == original_binary
            assert repaired.ir_version == original.ir_version == 13
            onnx.checker.check_model(repaired)
            report['components'][component] = {
                'original_check': check(original), 'previous_prepared_check': check(prepared),
                'repaired_check': check(repaired), 'ir_version': repaired.ir_version,
                'original_opsets': [[o.domain, o.version] for o in original.opset_import],
                'repaired_opsets': [[o.domain, o.version] for o in repaired.opset_import],
                'binary_sha256': sha(original_binary), 'binary_bytes_unchanged': True,
                'previous_wrapper_only_filename_changed': True,
                'repair_only_adds_domain_import': True,
                'repaired_wrapper_sha256': sha(repaired.SerializeToString()),
                'inputs': len(repaired.graph.input), 'outputs': len(repaired.graph.output)}
    jobs_bytes = (out / 'jobs.json').read_bytes()
    jobs = json.loads(jobs_bytes)
    entries = list(jobs['components'].values()) + jobs['history']
    report['jobs_sha256'] = sha(jobs_bytes)
    report['failed_remote_jobs'] = sorted({e['jobId'] for e in entries if e['status'] == 'FAILED'})
    profile = json.loads((out / 'encoder-j568wnxyg-profile.json').read_text())
    times = profile['execution_summary']['all_inference_times']
    report['encoder_profile'] = {'runs': len(times), 'median_us': statistics.median(times),
        'execution_details': len(profile['execution_detail']),
        'npu_details': sum(e['compute_unit'] == 'NPU' for e in profile['execution_detail'])}
    (out / 'local-packaging-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k != 'metadata'}, indent=2))


if __name__ == '__main__':
    main()
