#!/usr/bin/env bash
set -euo pipefail

output_root=${1:?An output directory is required.}
commit=${GITHUB_SHA:?GITHUB_SHA is required for comparable weekly artifacts.}
mkdir -p "$output_root"
source_directories=()
cleanup_sources() { for directory in "${source_directories[@]}"; do rmdir "$directory" 2>/dev/null || true; done; }
trap cleanup_sources EXIT

run_suite() {
  local label=$1 filter=$2 source_parent=$3
  shift 3
  mkdir -p "$source_parent"
  local source_json
  source_json=$(mktemp -d "$source_parent/weekly-$commit.XXXXXX")
  source_directories+=("$source_json")
  local arguments=() argument
  for argument in "$@"; do arguments+=("${argument//__SOURCE_JSON__/$source_json}"); done
  local result_dir="$output_root/$label/trx"
  env "${arguments[@]}" dotnet test tests/IntegrationTests/IntegrationTests.csproj \
    --configuration Release --no-restore --no-build --filter "$filter" \
    --logger "trx;LogFileName=$label-$commit.trx" --results-directory "$result_dir"
  scripts/assert-trx-tests-ran.sh "$result_dir"
  scripts/assert-json-artifacts.sh "$source_json"
  mkdir -p "$output_root/$label/json"
  cp "$source_json"/*.json "$output_root/$label/json/"
  rm -f "$source_json"/*.json
}

run_suite api-latency 'FullyQualifiedName~ApiLatencyBenchmarkTests' /tmp/eiams-api-latency-results \
  RUN_API_BENCHMARKS=1 EIAMS_BENCHMARK_SAMPLES=10 EIAMS_BENCHMARK_RESULTS_DIR=__SOURCE_JSON__
run_suite audit-overhead 'FullyQualifiedName~AuditOverheadTests.AuditCapture_Should_RecordEveryPostedMovement_ForOneThousandLines' /tmp/eiams-audit-overhead-results \
  RUN_PERFORMANCE_TESTS=1 EIAMS_AUDIT_OVERHEAD_RESULT_DIR=__SOURCE_JSON__
run_suite synthetic-dataset-small 'FullyQualifiedName~SyntheticDatasetSeederTests.SeedSmallAsync' /tmp/eiams-synthetic-dataset-results \
  RUN_SYNTHETIC_DATASET_TESTS=1 EIAMS_SYNTHETIC_DATASET_RESULT_DIR=__SOURCE_JSON__
run_suite kdf-quick 'FullyQualifiedName~KdfBenchmarkTests.MeasureBoundedKdfVerificationCostAsync' "/tmp/eiams-kdf-benchmark-results/weekly-$commit" \
  RUN_KDF_BENCHMARK=1 EIAMS_KDF_BENCHMARK_PROFILE=QuickValidation EIAMS_KDF_BENCHMARK_RESULT_DIR=__SOURCE_JSON__
run_suite pg-statements-quick 'FullyQualifiedName~PgStatStatementsHarnessIntegrationTests.QuickHarness' "/tmp/eiams-pg-stat-statements-results/weekly-$commit" \
  RUN_PG_STAT_STATEMENTS_HARNESS=1 EIAMS_PG_STAT_STATEMENTS_PROFILE=Quick EIAMS_PG_STAT_STATEMENTS_RESULT_DIR=__SOURCE_JSON__
run_suite read-plan-quick 'FullyQualifiedName~PostgreSqlReadPlanAnalysisIntegrationTests.QuickAnalysis' "/tmp/eiams-postgres-read-plan-results/weekly-$commit" \
  RUN_POSTGRES_READ_PLAN_ANALYSIS=1 EIAMS_POSTGRES_PLAN_PROFILE=Quick EIAMS_POSTGRES_PLAN_RESULT_DIR=__SOURCE_JSON__
run_suite maintenance-quick 'FullyQualifiedName~PostgreSqlMaintenanceHarnessIntegrationTests.QuickHarness' "/tmp/eiams-postgres-maintenance-results/weekly-$commit" \
  RUN_POSTGRES_MAINTENANCE_HARNESS=1 EIAMS_POSTGRES_MAINTENANCE_PROFILE=Quick EIAMS_POSTGRES_MAINTENANCE_RESULT_DIR=__SOURCE_JSON__
run_suite write-scale-quick 'FullyQualifiedName~WriteScaleBenchmarkTests.MeasureBoundedDocumentPostingScaleAsync' "/tmp/eiams-write-scale-benchmark-results/weekly-$commit" \
  RUN_WRITE_SCALE_BENCHMARK=1 EIAMS_WRITE_SCALE_BENCHMARK_PROFILE=QuickValidation EIAMS_WRITE_SCALE_BENCHMARK_RESULT_DIR=__SOURCE_JSON__
run_suite api-load-quick 'FullyQualifiedName~ApiLoadTestSmokeTests.RunExplicitConfigurableApiLoadSuiteAsync' "/tmp/eiams-api-load-results/weekly-$commit" \
  RUN_API_LOAD_SUITE=1 EIAMS_API_LOAD_PROFILE=QuickValidation EIAMS_API_LOAD_DATASET=Small EIAMS_API_LOAD_SEED=20260909 EIAMS_API_LOAD_WORKER_PROFILE=IsolatedRequestCost EIAMS_API_LOAD_RESULT_DIR=__SOURCE_JSON__
