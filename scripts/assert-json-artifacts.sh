#!/usr/bin/env bash
set -euo pipefail
directory=${1:?A JSON artifact directory is required.}
python3 - "$directory" <<'PY'
import glob, json, os, sys
directory = sys.argv[1]
files = glob.glob(os.path.join(directory, "**", "*.json"), recursive=True)
if not files:
    raise SystemExit(f"No JSON evidence was produced in {directory}.")
for path in files:
    with open(path, encoding="utf-8") as source:
        value = source.read()
    json.loads(value)
    lowered = value.lower()
    for forbidden in ("password=", "pwd=", "host=", "postgres://", "postgresql://", "user id=", "username=", "connectionstring", "databaseurl", "storagekey", "storage_key", "bearer "):
        if forbidden in lowered:
            raise SystemExit(f"JSON evidence contains a forbidden sensitive marker: {path}")
    import re
    if re.search(r'"(?:password|pwd|username|user_id|connectionstring|databaseurl|storage_key|bearer|refresh_token|email)"\s*:', lowered):
        raise SystemExit(f"JSON evidence contains a forbidden sensitive key: {path}")
print(f"Verified {len(files)} valid, redacted JSON evidence file(s).")
PY
