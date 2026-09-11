#!/usr/bin/env bash
set -euo pipefail
workflow=${1:-.github/workflows/build.yml}
python3 - "$workflow" <<'PY'
import re
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as source:
    text = source.read()

# GitHub remains the authoritative YAML parser. This deliberately dependency-free contract
# validator prevents CI regressions on local machines and on the hosted runner.
if "\t" in text:
    raise SystemExit("Workflow YAML must not contain tab indentation.")

required = [
    "pull_request:", "permissions:\n  contents: read", "fast-pr:", "postgres-integration:",
    "needs: fast-pr", "timeout-minutes: 25", "timeout-minutes: 35", "docker info",
    "Category!=Performance", "Category=Performance",
    "scripts/assert-trx-tests-ran.sh artifacts/test-results/unit",
    "scripts/assert-trx-tests-ran.sh artifacts/test-results/architecture",
    "scripts/assert-trx-tests-ran.sh artifacts/test-results/security-contracts",
    "scripts/assert-trx-tests-ran.sh artifacts/test-results/postgres-integration",
    "dotnet publish CleanArchitectureTemplate.slnx --configuration Release --no-restore --no-build",
]
missing = [value for value in required if value not in text]
if missing:
    raise SystemExit(f"CI workflow contract missing: {', '.join(missing)}")
if "secrets." in text:
    raise SystemExit("PR workflow must not reference repository secrets.")

for action in re.findall(r"^\s*- uses: (\S+)", text, re.MULTILINE):
    if not re.fullmatch(r"[^@]+@[0-9a-f]{40}", action):
        raise SystemExit(f"Action is not pinned to a full SHA: {action}")

print("CI workflow contract is valid.")
PY
