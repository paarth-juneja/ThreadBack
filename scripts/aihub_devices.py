"""Read-only AI Hub device discovery. API key arrives on stdin, never argv/logs."""
import json
import logging
import sys
from datetime import datetime, timezone
from pathlib import Path

import qai_hub as hub


def main():
    logging.disable(logging.CRITICAL)
    token = sys.stdin.readline().strip()
    if not token:
        print("No key received.", file=sys.stderr)
        return 1
    try:
        client = hub.Client(hub.ClientConfig(api_token=token))
        devices = client.get_devices()
        rows = [
            {"name": d.name, "os": d.os, "attributes": list(d.attributes)}
            for d in devices
        ]
    except Exception as error:
        # Do not print exception text or tracebacks that could include request details.
        print(f"AI Hub connection failed ({type(error).__name__}). No jobs were submitted.", file=sys.stderr)
        return 1
    finally:
        token = None
    destination = Path(__file__).resolve().parents[1] / "artifacts" / "verification" / "aihub-devices.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps({
        "checkedAt": datetime.now(timezone.utc).isoformat(),
        "devices": rows,
        "jobsSubmitted": False,
    }, indent=2), encoding="utf-8")
    print(f"AI Hub returned {len(rows)} device entries.")
    for row in rows:
        if any(word in (row["name"] + " " + row["os"] + " " + " ".join(row["attributes"])).lower() for word in ("windows", "snapdragon x", "compute")):
            print("  " + row["name"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
