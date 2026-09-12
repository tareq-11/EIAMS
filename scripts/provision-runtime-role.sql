-- EIAMS runtime database role provisioning (PostgreSQL / psql).
--
-- Run this ONLY after EF migrations, while connected as the role that owns migration-created
-- objects. It never sets a password: provision that credential separately in the deployment
-- secret store. The application must use the resulting runtime role connection string and must
-- never run this script or migrations during production startup.
--
-- Example (identifiers, not secrets):
--   psql "$MIGRATION_OWNER_CONNECTION" \
--     -v runtime_role=eiams_runtime \
--     -v migration_owner=eiams_migrator \
--     -v database_name=eiams \
--     -f scripts/provision-runtime-role.sql
--
-- The migration owner must retain ownership of the schema, tables, functions, and triggers.
-- This script deliberately grants no DDL, ownership, role-administration, or trigger-management
-- capability to the runtime role.

\set ON_ERROR_STOP on

\if :{?runtime_role}
\else
  \set runtime_role eiams_runtime
\endif

\if :{?migration_owner}
\else
  \set migration_owner :USER
\endif

\if :{?database_name}
\else
  \set database_name :DBNAME
\endif

SELECT current_user = :'migration_owner' AS is_migration_owner \gset
\if :is_migration_owner
\else
  \echo 'ERROR: connect as the migration owner (migration_owner must equal current_user).'
  \quit 3
\endif

-- A deployment typo such as runtime_role=postgres must never mutate the migration owner. Role
-- names are intentionally limited to ordinary lower-case PostgreSQL identifiers, excluding
-- PostgreSQL-reserved pg_* names and the conventional postgres administrator name.
SELECT
    :'runtime_role' <> current_user
    AND :'runtime_role' <> :'migration_owner'
    AND :'runtime_role' ~ '^[a-z][a-z0-9_]{0,62}$'
    AND :'runtime_role' !~ '^(postgres|pg_)' AS runtime_role_name_is_safe
\gset
\if :runtime_role_name_is_safe
\else
  \echo 'ERROR: runtime_role is unsafe, reserved, or equals the migration owner/current user.'
  \quit 3
\endif

-- NOINHERIT alone is insufficient: a member can still SET ROLE to any role granted to it. Do not
-- guess which memberships are harmless; fail until deployment removes them explicitly.
SELECT NOT EXISTS (
    SELECT 1
    FROM pg_auth_members AS membership
    INNER JOIN pg_roles AS member ON member.oid = membership.member
    WHERE member.rolname = :'runtime_role'
) AS runtime_has_no_memberships
\gset
\if :runtime_has_no_memberships
\else
  \echo 'ERROR: runtime_role has role memberships. Revoke them before provisioning.'
  \quit 3
\endif

-- Table/schema ownership bypasses ordinary grants and would permit ALTER TABLE / trigger control.
-- This script never transfers ownership automatically: stop and remediate it in a reviewed
-- migration/deployment operation first.
SELECT NOT EXISTS (
    SELECT 1
    FROM pg_namespace AS schema
    INNER JOIN pg_roles AS owner ON owner.oid = schema.nspowner
    WHERE schema.nspname = 'public'
      AND owner.rolname = :'runtime_role'

    UNION ALL

    SELECT 1
    FROM pg_class AS relation
    INNER JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
    INNER JOIN pg_roles AS owner ON owner.oid = relation.relowner
    WHERE schema.nspname = 'public'
      AND owner.rolname = :'runtime_role'
      AND relation.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')

    UNION ALL

    -- A trigger function owner can CREATE OR REPLACE that function and bypass the trigger's
    -- invariant, even when the role owns no table.
    SELECT 1
    FROM pg_proc AS routine
    INNER JOIN pg_namespace AS schema ON schema.oid = routine.pronamespace
    INNER JOIN pg_roles AS owner ON owner.oid = routine.proowner
    WHERE schema.nspname = 'public'
      AND owner.rolname = :'runtime_role'

    UNION ALL

    -- Relation row types have typrelid <> 0 and array helper types have typelem <> 0; exclude
    -- both implicit kinds while rejecting standalone user types and domains in the app schema.
    SELECT 1
    FROM pg_type AS data_type
    INNER JOIN pg_namespace AS schema ON schema.oid = data_type.typnamespace
    INNER JOIN pg_roles AS owner ON owner.oid = data_type.typowner
    WHERE schema.nspname = 'public'
      AND owner.rolname = :'runtime_role'
      AND data_type.typrelid = 0
      AND data_type.typelem = 0

    UNION ALL

    SELECT 1
    FROM pg_database AS database
    INNER JOIN pg_roles AS owner ON owner.oid = database.datdba
    WHERE database.datname = current_database()
      AND owner.rolname = :'runtime_role'
) AS runtime_owns_no_application_objects
\gset
\if :runtime_owns_no_application_objects
\else
  \echo 'ERROR: runtime_role owns application schema objects, routines/types, or the current database. Transfer ownership to migration_owner, then retry.'
  \quit 3
\endif

BEGIN;

-- Idempotent role creation. LOGIN has no password until the deployment secret manager supplies
-- one. NOINHERIT prevents accidental privilege inheritance through group membership.
SELECT format(
    'CREATE ROLE %I LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION',
    :'runtime_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_role')
\gexec

-- Harden on EVERY run, including when the role pre-existed with excessive attributes. Passwords
-- remain external to this script and are never changed here.
SELECT format(
    'ALTER ROLE %I LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
    :'runtime_role')
\gexec
SELECT
    rolcanlogin
    AND NOT rolinherit
    AND NOT rolsuper
    AND NOT rolcreatedb
    AND NOT rolcreaterole
    AND NOT rolreplication
    AND NOT rolbypassrls AS runtime_role_is_hardened
FROM pg_roles
WHERE rolname = :'runtime_role'
\gset
\if :runtime_role_is_hardened
\else
  ROLLBACK;
  \echo 'ERROR: runtime_role attributes could not be hardened.'
  \quit 3
\endif

SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'database_name', :'runtime_role')
\gexec
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'runtime_role')
\gexec
-- PostgreSQL can grant CREATE on public to PUBLIC. A role-specific REVOKE cannot override that
-- inherited ACL, therefore public schema creation is intentionally disabled database-wide. Any
-- legitimate DDL identity must be the schema owner or receive an explicit reviewed grant.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('REVOKE CREATE ON SCHEMA public FROM %I', :'runtime_role')
\gexec

-- Existing application objects. New tables/sequences created by this migration owner receive
-- matching permissions from ALTER DEFAULT PRIVILEGES below.
SELECT format(
    'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I',
    :'runtime_role')
\gexec
SELECT format(
    'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I',
    :'runtime_role')
\gexec

-- Runtime never migrates. It may read the EF history for diagnostics, but cannot alter it.
SELECT format('REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM %I', :'runtime_role')
WHERE to_regclass('public."__EFMigrationsHistory"') IS NOT NULL
\gexec
SELECT format('GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO %I', :'runtime_role')
WHERE to_regclass('public."__EFMigrationsHistory"') IS NOT NULL
\gexec

-- Trigger-protected append-only ledgers: application code can append and read them, but cannot
-- update/delete them even if a trigger were accidentally weakened. ALTER ... DISABLE TRIGGER is
-- also impossible because the runtime role owns neither these tables nor their schema.
SELECT format('REVOKE UPDATE, DELETE ON TABLE public.%I FROM %I', immutable_table, :'runtime_role')
FROM unnest(ARRAY[
    'audit_logs',
    'audit_log_entries',
    'stock_movements',
    'asset_movement_history',
    'document_lifecycle_events'
]) AS immutable_table
WHERE to_regclass(format('public.%I', immutable_table)) IS NOT NULL
\gexec

-- Future migration-created objects. When a future migration adds another append-only table, add
-- the same targeted REVOKE in that migration/deployment step; broad defaults are intentionally
-- not treated as a substitute for that table-specific invariant.
SELECT format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
    :'migration_owner', :'runtime_role')
\gexec
SELECT format(
    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO %I',
    :'migration_owner', :'runtime_role')
\gexec

COMMIT;

-- Verification queries are read-only and intentionally reveal only privilege metadata.
SELECT
    :'runtime_role' AS runtime_role,
    has_schema_privilege(:'runtime_role', 'public', 'USAGE') AS can_use_public_schema,
    has_schema_privilege(:'runtime_role', 'public', 'CREATE') AS can_create_in_public_schema;

SELECT tablename, tableowner
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('audit_logs', 'stock_movements', '__EFMigrationsHistory')
ORDER BY tablename;
