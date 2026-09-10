#!/usr/bin/env bash
set -euo pipefail

# Normalizes the deliberate unhealthy-index preflight failure to exit code 5. The SQL file
# itself uses an intentional SQL error because the installed psql does not support `\quit <n>`.

if [[ "${EIAMS_MIGRATION_TARGET_NON_NEON:-}" != "1" ]]; then
  echo "Refusing: set EIAMS_MIGRATION_TARGET_NON_NEON=1 only for local or approved non-Neon staging." >&2
  exit 3
fi

if [[ -z "${MIGRATION_DATABASE_URL:-}" ]]; then
  echo "Refusing: MIGRATION_DATABASE_URL must be supplied by a secret store or environment." >&2
  exit 3
fi

connection_authority="${MIGRATION_DATABASE_URL#*://}"
connection_authority="${connection_authority%%/*}"
connection_host_port="${connection_authority##*@}"
connection_host="${connection_host_port%%:*}"
if [[ "$MIGRATION_DATABASE_URL" != *"://"* || -z "$connection_host" || "${connection_host,,}" == *neon* ]]; then
  echo "Refusing: this preflight runner permits only an explicit non-Neon PostgreSQL URL." >&2
  exit 3
fi

script_directory="$(cd -- "$(dirname -- "$0")" && pwd)"
output_file="$(mktemp)"
trap 'rm -f "$output_file"' EXIT

set +e
psql "$MIGRATION_DATABASE_URL" -X -v ON_ERROR_STOP=1 \
  -v eiams_migration_preflight_non_neon=1 \
  -f "$script_directory/migration-preflight.sql" >"$output_file" 2>&1
psql_status=$?
set -e
cat "$output_file"

if grep -Fq 'EIAMS_PREFLIGHT_UNHEALTHY_INDEXES' "$output_file"; then
  exit 5
fi

exit "$psql_status"
