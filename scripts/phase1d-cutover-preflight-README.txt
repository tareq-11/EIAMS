Phase 1D assignment-scope cutover preflight

Run without a database (produces WARN/UNAVAILABLE):
  ./scripts/run-phase1d-cutover-preflight.sh

Run only against an approved non-production restore. Credentials are passed to
libpq through environment variables and never as a URI or command-line value:
  EIAMS_PHASE1D_ALLOW_NON_PRODUCTION=1 \
  EIAMS_PHASE1D_PGHOST="$PGHOST" EIAMS_PHASE1D_PGDATABASE="$PGDATABASE" \
  EIAMS_PHASE1D_PGUSER="$PGUSER" EIAMS_PHASE1D_PGPASSWORD="$PGPASSWORD" \
  ./scripts/run-phase1d-cutover-preflight.sh

The default artifact is contracts/baseline/phase1d-cutover-preflight.json.
Exit 0 means all checks PASS; 2 means a completed check observed BLOCK; 3 is a
safety refusal; 4 means WARN or unavailable database checks. The artifact is
atomic and contains counts/statuses only. It does not identify users or expose
secrets. OrganizationalUnit assignments are never converted or deleted: after
backup/restore verification, the business owner must remediate each user to
Enterprise, Site, Warehouse, or suspension before applying the 1D migration.
Suspended users may intentionally have zero assignments; active users require
exactly one, and every user (including suspended users) must have no more than
one assignment.
Nonzero OU rows in role_allowed_scope_types or permission_allowed_scope_types
are WARN (the reviewed migration removes them deterministically); they may be
present when there are no BLOCK checks, but the post-migration preflight must
return PASS. User OU assignments, zero/multiple assignments, invalid targets,
and role/scope incompatibility remain BLOCK and must be remediated first.
The WARN exit (4) for planned allowed-scope cleanup is review-required, not a
permission to skip the reviewed migration; proceed only when there are no
BLOCK rows, then rerun this preflight and require PASS after migration.
The migration has a fail-closed guard and refuses any remaining OU user
assignment. OrganizationalUnit remains a business entity/resource and custody
holder; this preflight concerns user assignment rows only.
