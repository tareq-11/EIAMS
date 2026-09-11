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
    "cron: \"0 2 * * 0\"", "scheduled-performance:", "timeout-minutes: 45",
    "Category!=Performance", "scripts/run-weekly-performance-checks.sh artifacts/performance/${{ github.sha }}",
    "name: weekly-performance-${{ github.sha }}", "retention-days: 30",
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
if not re.search(r"Retain scheduled performance results\n\s+if: always\(\)", text):
    raise SystemExit("Weekly performance evidence upload must use if: always().")

for action in re.findall(r"^\s*- uses: (\S+)", text, re.MULTILINE):
    if not re.fullmatch(r"[^@]+@[0-9a-f]{40}", action):
        raise SystemExit(f"Action is not pinned to a full SHA: {action}")

print("CI workflow contract is valid.")

with open("scripts/run-weekly-performance-checks.sh", encoding="utf-8") as source:
    weekly = source.read()
for gate in ("RUN_API_BENCHMARKS=1", "RUN_PERFORMANCE_TESTS=1", "RUN_KDF_BENCHMARK=1",
             "RUN_PG_STAT_STATEMENTS_HARNESS=1", "RUN_POSTGRES_READ_PLAN_ANALYSIS=1",
             "RUN_POSTGRES_MAINTENANCE_HARNESS=1", "RUN_WRITE_SCALE_BENCHMARK=1", "RUN_API_LOAD_SUITE=1"):
    if gate not in weekly:
        raise SystemExit(f"Weekly Quick gate missing: {gate}")
for forbidden in ("EIAMS_ALLOW_LONG", "=FullBaseline", "_PROFILE=Scale", "_PROFILE=Medium"):
    if forbidden in weekly:
        raise SystemExit(f"Weekly script must not enable long profile: {forbidden}")
if "RUN_SYNTHETIC_DATASET_TESTS=1" not in weekly:
    raise SystemExit("Weekly Quick gate missing: RUN_SYNTHETIC_DATASET_TESTS=1")
if weekly.count("run_suite ") < 9 or "scripts/assert-trx-tests-ran.sh" not in weekly or "scripts/assert-json-artifacts.sh" not in weekly:
    raise SystemExit("Every weekly Quick suite must require TRX execution and JSON evidence.")
print("Weekly performance contract is valid.")
PY
