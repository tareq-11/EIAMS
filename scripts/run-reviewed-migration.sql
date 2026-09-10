-- Executes one reviewed, version-bounded NON-idempotent EF migration script.
--
-- This file is invoked by run-reviewed-migration.sh, which rejects idempotent `DO $EF$`
-- wrappers before psql is started. Do not invoke it against Neon or production.
-- The temporary advisory lock remains held from preflight through postflight and is released on
-- normal completion or automatically when psql disconnects after an error.

\set ON_ERROR_STOP on
\set migration_max_transaction_age_seconds 60

\if :{?eiams_migration_non_neon}
\else
  \warn Refusing to run: set EIAMS_MIGRATION_TARGET_NON_NEON=1 only after verifying a local or approved non-Neon staging target.
  SELECT 1 / 0;
\endif

\if :eiams_migration_non_neon
\else
  \warn Refusing to run: eiams_migration_non_neon must be 1.
  SELECT 1 / 0;
\endif

\if :{?migration_script}
\else
  \warn Refusing to run: a reviewed migration_script path is required.
  SELECT 1 / 0;
\endif

\if :{?migration_from}
\else
  \warn Refusing to run: migration_from is required.
  SELECT 1 / 0;
\endif

\if :{?migration_to}
\else
  \warn Refusing to run: migration_to is required.
  SELECT 1 / 0;
\endif

SELECT pg_try_advisory_lock(hashtextextended('eiams:ef-migrations', 0)) AS migration_lock_acquired \gset
\if :migration_lock_acquired
\else
  \warn Refusing to run: another migration runner holds the EIAMS migration advisory lock.
  SELECT 1 / 0;
\endif

-- The history bounds prevent an operator from applying a reviewed A-to-B file to an unexpected
-- database state. A separate migration/recovery decision is required if B is already present.
SELECT EXISTS (
    SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = :'migration_from'
) AS migration_from_applied \gset
\if :migration_from_applied
\else
  \warn Refusing to run: migration_from is not present in __EFMigrationsHistory.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  SELECT 1 / 0;
\endif

SELECT EXISTS (
    SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = :'migration_to'
) AS migration_to_already_applied \gset
\if :migration_to_already_applied
  \warn Refusing to run: migration_to is already present in __EFMigrationsHistory.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  SELECT 1 / 0;
\endif

SELECT EXISTS (
    SELECT 1
    FROM pg_stat_activity
    WHERE datname = current_database()
      AND xact_start IS NOT NULL
      AND pid <> pg_backend_pid()
      AND now() - xact_start > make_interval(secs => :migration_max_transaction_age_seconds)
) AS long_transactions_present \gset
\if :long_transactions_present
  \warn Refusing to run: a transaction older than the migration threshold is active.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  SELECT 1 / 0;
\endif

SELECT EXISTS (
    SELECT 1
    FROM pg_stat_activity AS waiting
    JOIN pg_locks AS waiting_lock ON waiting_lock.pid = waiting.pid AND NOT waiting_lock.granted
    WHERE waiting.datname = current_database()
) AS blocked_sessions_present \gset
\if :blocked_sessions_present
  \warn Refusing to run: blocked database sessions are present; investigate before long DDL.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  SELECT 1 / 0;
\endif

SELECT EXISTS (
    SELECT 1 FROM pg_index AS i WHERE NOT i.indisvalid OR NOT i.indisready
) AS unhealthy_indexes \gset
\if :unhealthy_indexes
  \warn Refusing to run: invalid or not-ready indexes exist; recover them manually first.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  \echo EIAMS_RUNNER_UNHEALTHY_INDEXES
  SELECT 1 / 0;
\endif

\echo 'Preflight passed. Applying the reviewed non-idempotent migration script while holding the advisory lock.'
\i :migration_script

SELECT EXISTS (
    SELECT 1 FROM pg_index AS i WHERE NOT i.indisvalid OR NOT i.indisready
) AS unhealthy_indexes_after \gset
\if :unhealthy_indexes_after
  \warn Migration finished with an invalid or not-ready index. Do not retry blindly; follow the recovery runbook.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  \echo EIAMS_RUNNER_UNHEALTHY_INDEXES
  SELECT 1 / 0;
\endif

SELECT EXISTS (
    SELECT 1 FROM public."__EFMigrationsHistory" WHERE migration_id = :'migration_to'
) AS migration_to_applied \gset
\if :migration_to_applied
\else
  \warn Migration script returned without the expected migration_to history entry.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0));
  SELECT 1 / 0;
\endif

SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0)) AS migration_lock_released;
\echo 'Migration runner completed successfully.'
