-- EIAMS PostgreSQL migration preflight (read-only catalog inspection plus a temporary advisory lock).
--
-- This script NEVER runs migrations, creates indexes, drops indexes, reindexes, or claims that a
-- backup exists. Run it immediately before AND immediately after a dedicated migration-owner job.
--
-- Refuses to run unless an operator explicitly confirms the target is not Neon. This is an
-- intentional client-side safety gate; it is not a technical proof of the provider identity.
-- Example (only against a local or approved non-Neon staging database):
--   psql "$MIGRATION_DATABASE_URL" -X -v ON_ERROR_STOP=1 \
--     -v eiams_migration_preflight_non_neon=1 -f scripts/migration-preflight.sql
--
-- Do not pass a production or Neon URL to this script. A migration owner, rather than the
-- restricted runtime role, must run it because pg_catalog visibility can otherwise be incomplete.

\set ON_ERROR_STOP on

\if :{?eiams_migration_preflight_non_neon}
\else
  \warn Refusing to run: explicitly set -v eiams_migration_preflight_non_neon=1 after verifying a local or approved non-Neon target.
  SELECT 1 / 0;
\endif

\if :eiams_migration_preflight_non_neon
\else
  \warn Refusing to run: eiams_migration_preflight_non_neon must be 1.
  SELECT 1 / 0;
\endif

-- The same resource is used by the optional Development startup runner. A session advisory lock
-- is released automatically when psql exits, including on an ON_ERROR_STOP failure.
SELECT pg_try_advisory_lock(hashtextextended('eiams:ef-migrations', 0)) AS migration_lock_acquired \gset

\if :migration_lock_acquired
\else
  \warn Refusing to run: another migration runner holds the EIAMS migration advisory lock.
  SELECT 1 / 0;
\endif

\echo '== Target and capacity signals (signals only; not a backup or free-disk guarantee) =='
SELECT
    current_database() AS database_name,
    current_user AS migration_owner,
    version() AS server_version,
    pg_size_pretty(pg_database_size(current_database())) AS database_size,
    current_setting('server_version_num') AS server_version_num,
    current_setting('max_connections') AS max_connections,
    current_setting('maintenance_work_mem') AS maintenance_work_mem;

-- PostgreSQL does not expose reliable host free-disk capacity to normal SQL roles. These are
-- allocation signals only; operators must separately verify provider/host disk headroom and a
-- tested restore before long DDL.
SELECT
    n.nspname AS schema_name,
    c.relname AS relation_name,
    pg_size_pretty(pg_total_relation_size(c.oid)) AS allocated_size
FROM pg_class AS c
JOIN pg_namespace AS n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind IN ('r', 'm')
ORDER BY pg_total_relation_size(c.oid) DESC
LIMIT 20;

\echo '== Active long transactions (review before migration) =='
SELECT
    pid,
    usename,
    application_name,
    state,
    now() - xact_start AS transaction_age,
    wait_event_type,
    wait_event
FROM pg_stat_activity
WHERE datname = current_database()
  AND xact_start IS NOT NULL
  AND pid <> pg_backend_pid()
ORDER BY xact_start;

\echo '== Currently blocked sessions (review before migration) =='
SELECT
    waiting.pid AS waiting_pid,
    blocking.pid AS blocking_pid,
    waiting.usename AS waiting_user,
    blocking.usename AS blocking_user,
    waiting.application_name AS waiting_application,
    blocking.application_name AS blocking_application,
    now() - waiting.query_start AS waiting_age,
    waiting.wait_event_type,
    waiting.wait_event
FROM pg_stat_activity AS waiting
JOIN pg_locks AS waiting_lock
  ON waiting_lock.pid = waiting.pid
 AND NOT waiting_lock.granted
JOIN pg_locks AS blocking_lock
  ON blocking_lock.locktype = waiting_lock.locktype
 AND blocking_lock.database IS NOT DISTINCT FROM waiting_lock.database
 AND blocking_lock.relation IS NOT DISTINCT FROM waiting_lock.relation
 AND blocking_lock.page IS NOT DISTINCT FROM waiting_lock.page
 AND blocking_lock.tuple IS NOT DISTINCT FROM waiting_lock.tuple
 AND blocking_lock.virtualxid IS NOT DISTINCT FROM waiting_lock.virtualxid
 AND blocking_lock.transactionid IS NOT DISTINCT FROM waiting_lock.transactionid
 AND blocking_lock.classid IS NOT DISTINCT FROM waiting_lock.classid
 AND blocking_lock.objid IS NOT DISTINCT FROM waiting_lock.objid
 AND blocking_lock.objsubid IS NOT DISTINCT FROM waiting_lock.objsubid
 AND blocking_lock.pid <> waiting_lock.pid
 AND blocking_lock.granted
JOIN pg_stat_activity AS blocking ON blocking.pid = blocking_lock.pid
WHERE waiting.datname = current_database()
ORDER BY waiting.query_start;

\echo '== Invalid or not-ready indexes (must be empty before and after migration) =='
SELECT
    n.nspname AS schema_name,
    c.relname AS index_name,
    t.relname AS table_name,
    i.indisvalid AS is_valid,
    i.indisready AS is_ready
FROM pg_index AS i
JOIN pg_class AS c ON c.oid = i.indexrelid
JOIN pg_class AS t ON t.oid = i.indrelid
JOIN pg_namespace AS n ON n.oid = c.relnamespace
WHERE NOT i.indisvalid OR NOT i.indisready
ORDER BY n.nspname, c.relname;

SELECT EXISTS (
    SELECT 1
    FROM pg_index AS i
    WHERE NOT i.indisvalid OR NOT i.indisready
) AS unhealthy_indexes \gset

\if :unhealthy_indexes
  \warn Refusing migration: invalid or not-ready indexes were found. Recover them manually before retrying.
  SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0)) AS migration_lock_released;
  \echo EIAMS_PREFLIGHT_UNHEALTHY_INDEXES
  SELECT 1 / 0;
\endif

\echo '== EF migration history (record only; compare with migration A before generating a bounded non-idempotent deployment script) =='
SELECT migration_id, product_version
FROM public."__EFMigrationsHistory"
ORDER BY migration_id;

SELECT pg_advisory_unlock(hashtextextended('eiams:ef-migrations', 0)) AS migration_lock_released;
