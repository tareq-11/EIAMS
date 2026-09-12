#!/usr/bin/env bash
set -euo pipefail

# Safe wrapper for the one migration chain that contains CREATE INDEX CONCURRENTLY.
# It never prints the database URL or reads a password from command-line arguments.

if [[ "${EIAMS_MIGRATION_TARGET_NON_NEON:-}" != "1" ]]; then
  echo "Refusing: set EIAMS_MIGRATION_TARGET_NON_NEON=1 only for local or approved non-Neon staging." >&2
  exit 3
fi

if [[ -z "${MIGRATION_DATABASE_URL:-}" ]]; then
  echo "Refusing: MIGRATION_DATABASE_URL must be supplied by a secret store or environment." >&2
  exit 3
fi

connection_host=""
if [[ "$MIGRATION_DATABASE_URL" == *"://"* ]]; then
  connection_authority="${MIGRATION_DATABASE_URL#*://}"
  connection_authority="${connection_authority%%/*}"
  connection_host_port="${connection_authority##*@}"
  connection_host="${connection_host_port%%:*}"
else
  shopt -s nocasematch
  if [[ "$MIGRATION_DATABASE_URL" =~ (^|\;)[[:space:]]*host=([^\;]+) ]]; then
    connection_host="${BASH_REMATCH[2]}"
  fi
  shopt -u nocasematch
fi

if [[ -z "$connection_host" ]]; then
  echo "Refusing: cannot determine the database host for the non-Neon safety gate." >&2
  exit 3
fi

if [[ "${connection_host,,}" == *neon* ]]; then
  echo "Refusing: Neon targets are not permitted by this local/staging migration runner." >&2
  exit 3
fi

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <migration-from> <migration-to> <reviewed-non-idempotent-script.sql>" >&2
  exit 2
fi

migration_from="$1"
migration_to="$2"
migration_script="$3"

if [[ ! -f "$migration_script" || ! -r "$migration_script" ]]; then
  echo "Refusing: reviewed migration script is not a readable regular file." >&2
  exit 2
fi

if grep -Fqi 'DO $EF$' "$migration_script"; then
  echo "Refusing: EF idempotent scripts use DO \$EF\$ and cannot deploy CREATE INDEX CONCURRENTLY." >&2
  exit 2
fi

if ! grep -Eqi 'CREATE[[:space:]]+INDEX[[:space:]]+CONCURRENTLY' "$migration_script"; then
  echo "Refusing: this runner is only for a reviewed script containing CREATE INDEX CONCURRENTLY." >&2
  exit 2
fi

script_directory="$(cd -- "$(dirname -- "$0")" && pwd)"
absolute_migration_script="$(cd -- "$(dirname -- "$migration_script")" && pwd)/$(basename -- "$migration_script")"

output_file="$(mktemp)"
trap 'rm -f "$output_file"' EXIT

set +e
psql "$MIGRATION_DATABASE_URL" -X -v ON_ERROR_STOP=1 \
  -v eiams_migration_non_neon=1 \
  -v migration_from="$migration_from" \
  -v migration_to="$migration_to" \
  -v migration_script="$absolute_migration_script" \
  -f "$script_directory/run-reviewed-migration.sql" >"$output_file" 2>&1
psql_status=$?
set -e
cat "$output_file"

if grep -Fq 'EIAMS_RUNNER_UNHEALTHY_INDEXES' "$output_file"; then
  exit 5
fi

exit "$psql_status"
