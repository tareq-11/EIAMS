#!/usr/bin/env bash
set -euo pipefail

if [[ "${RBAC_PREFLIGHT_NON_PRODUCTION:-}" != "1" ]]; then
  echo "Refusing: set RBAC_PREFLIGHT_NON_PRODUCTION=1 for a local/approved restore only." >&2
  exit 3
fi
if [[ -z "${RBAC_PREFLIGHT_DATABASE_URL:-}" ]]; then
  echo "Refusing: RBAC_PREFLIGHT_DATABASE_URL must be supplied externally." >&2
  exit 3
fi
authority="${RBAC_PREFLIGHT_DATABASE_URL#*://}"
host_port="${authority##*@}"
host="${host_port%%:*}"
if [[ "${RBAC_PREFLIGHT_DATABASE_URL}" != *"://"* || -z "$host" || "${host,,}" == *neon* || "${RBAC_PREFLIGHT_DATABASE_URL,,}" == *production* ]]; then
  echo "Refusing: only an explicit non-production, non-Neon PostgreSQL target is permitted." >&2
  exit 3
fi

root="$(cd -- "$(dirname -- "$0")/.." && pwd)"
exec psql "$RBAC_PREFLIGHT_DATABASE_URL" -X -v ON_ERROR_STOP=1 -f "$root/scripts/rbac-v1-preflight.sql"
