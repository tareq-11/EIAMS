#!/usr/bin/env bash
set -euo pipefail
results_directory=${1:?A TRX results directory is required.}
python3 - "$results_directory" <<'PY'
import glob
import os
import sys
import xml.etree.ElementTree as ET

directory = sys.argv[1]
files = glob.glob(os.path.join(directory, "*.trx"))
if not files:
    raise SystemExit(f"No TRX result was produced in {directory}.")
total = 0
executed = 0
for path in files:
    root = ET.parse(path).getroot()
    counters = next((node for node in root.iter() if node.tag.endswith("Counters")), None)
    if counters is None:
        raise SystemExit(f"TRX result has no counters: {path}")
    try:
        total += int(counters.attrib["total"])
        executed += int(counters.attrib["executed"])
    except (KeyError, ValueError) as error:
        raise SystemExit(f"TRX result has invalid test counters: {path}") from error
if total < 1 or executed < 1:
    raise SystemExit(f"Selected zero tests in {directory}; refusing a silent CI pass.")
print(f"Verified {executed} executed test(s) out of {total} selected from {len(files)} TRX file(s).")
PY
