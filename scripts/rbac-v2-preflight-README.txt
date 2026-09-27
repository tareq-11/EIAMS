RBAC 2A dotted-v1 cutover preflight

The runner is read-only and accepts PostgreSQL credentials through libpq
environment variables only. It never prints a URI or secret:

  RBAC_V2_ALLOW_NON_PRODUCTION=1 \
  RBAC_V2_PGHOST="$PGHOST" RBAC_V2_PGDATABASE="$PGDATABASE" \
  RBAC_V2_PGUSER="$PGUSER" RBAC_V2_PGPASSWORD="$PGPASSWORD" \
  ./scripts/run-rbac-v2-preflight.sh

Without a target the result is WARN/UNAVAILABLE (exit 4). Legacy catalog,
grant, and scope rows are WARN because they are migration-managed; their
mapping and parity checks must be PASS before cleanup. Exit 2 means a blocking
mixed/unknown/missing-mapping/parity anomaly was observed; no cleanup or
migration is approved. Exit 0 means all checks passed. The output is
an atomic sanitized JSON artifact under contracts/baseline.

Mapping rows remain audit history only. They cannot translate a runtime request
or grant a role permission. The 2A migration validates them before deleting
legacy permission catalog, scope, and role-grant rows.
