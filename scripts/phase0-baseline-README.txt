Phase 0A baseline

Run without a database (source/OpenAPI inventory only):

  ./scripts/run-phase0-baseline.sh

Run database checks against an approved non-production, non-Neon PostgreSQL restore.
The connection is supplied through libpq environment variables, not a URI argument
(so the password/URI cannot appear in the psql process argv):

  EIAMS_BASELINE_ALLOW_NON_PRODUCTION=1 \
  EIAMS_BASELINE_PGHOST="$PGHOST" \
  EIAMS_BASELINE_PGDATABASE="$PGDATABASE" \
  EIAMS_BASELINE_PGUSER="$PGUSER" \
  EIAMS_BASELINE_PGPASSWORD="$PGPASSWORD" \
  ./scripts/run-phase0-baseline.sh

The default output is contracts/baseline/phase0-baseline.json. Set
EIAMS_BASELINE_OUTPUT to write another explicitly chosen path. The artifact contains
only route/file counts and anomaly counts; it does not contain row identifiers,
credentials, tokens, or response bodies.

Exit codes:

  0 = source inventory and supplied database checks passed
  2 = a database check returned BLOCK
  3 = safety/configuration refusal (for example missing target confirmation)
  4 = WARN/UNAVAILABLE (no database target or database preflight could not complete)

The SQL runs in BEGIN TRANSACTION READ ONLY and the wrapper forces
default_transaction_read_only=on, statement_timeout, and lock_timeout. It does not
run migrations or change schema/data. A WARN/UNAVAILABLE result is not evidence that
the database is clean. BLOCK means the SQL completed and observed a named anomaly;
UNAVAILABLE means connection/schema/query execution failed and is never converted to
PASS. The current query assumes the EF tables in the checked-in model; if a required
table/column is absent, psql failure is reported as unavailable and the artifact is
not interpreted as PASS. SQL cannot faithfully reproduce .NET NFKC, so that limitation
is recorded in the artifact and the canonical lower/trim plus ASCII shape checks remain
explicit. Null/blank, noncanonical, invalid-shape, duplicate-normalized, and historical
generated legacy usernames are release-blocking until explicitly remediated; application
startup does not rewrite them.
