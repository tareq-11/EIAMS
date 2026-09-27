\set ON_ERROR_STOP on

-- Phase 0A baseline only. This file is intentionally data/schema read-only.
-- It emits: section|status|count|note. The shell wrapper turns these rows into a
-- sanitized JSON artifact. Missing facts must be represented as UNAVAILABLE, never
-- guessed by this query.
BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '10000ms';
SET LOCAL lock_timeout = '2000ms';
\pset tuples_only on
\pset format unaligned
\pset fieldsep '|'

SELECT 'database' || '|' || 'PASS' || '|1|read-only transaction established';

SELECT 'users_without_exactly_one_assignment' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|active users with zero or any users with multiple assignments; suspended users may have zero'
FROM (
    SELECT u.id
    FROM public.users u
    LEFT JOIN public.user_role_scopes urs ON urs.user_id = u.id
    GROUP BY u.id, u.status
    HAVING count(urs.id) > 1 OR (u.status = 'Active' AND count(urs.id) = 0)
) invalid_users;

SELECT 'organizational_unit_user_assignments' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|user_role_scopes rows with OrganizationalUnit scope_type'
FROM public.user_role_scopes WHERE scope_type = 'OrganizationalUnit';

SELECT 'organizational_unit_role_allowed_scopes' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) ||
       '|role_allowed_scope_types rows with OrganizationalUnit'
FROM public.role_allowed_scope_types WHERE scope_type = 'OrganizationalUnit';

SELECT 'organizational_unit_permission_allowed_scopes' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) ||
       '|permission_allowed_scope_types rows with OrganizationalUnit'
FROM public.permission_allowed_scope_types
WHERE scope_type = 'OrganizationalUnit';

SELECT 'username_null_or_blank' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|users with null or blank username'
FROM public.users WHERE username IS NULL OR btrim(username) = '';

SELECT 'username_noncanonical' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|username differs from lower(trim(username)); SQL cannot reproduce .NET NFKC'
FROM public.users
WHERE username IS NOT NULL AND username IS DISTINCT FROM lower(btrim(username));

SELECT 'username_invalid_shape_or_length' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|username is outside the .NET 3..100 ASCII [a-z0-9._-] rule'
FROM public.users
WHERE username IS NULL OR username !~ '^[a-z0-9._-]{3,100}$';

SELECT 'username_legacy_generated' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|username uses the historical generated legacy-{id} convention and needs explicit remediation'
FROM public.users
WHERE username ~ '^legacy-[0-9a-f]{32}$';

SELECT 'username_duplicate_normalized' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|duplicate lower(trim(username)) groups'
FROM (SELECT lower(btrim(username)) AS normalized FROM public.users
      GROUP BY lower(btrim(username)) HAVING count(*) > 1) duplicates;

SELECT 'legacy_colon_permissions' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|permission codes containing a colon'
FROM public.permissions WHERE code LIKE '%:%';

SELECT 'non_dotted_non_legacy_permissions' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) ||
       '|permission codes that are neither dotted nor legacy-colon'
FROM public.permissions WHERE code NOT LIKE '%.%' AND code NOT LIKE '%:%';

SELECT 'material_family_missing_base_unit' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|families with a null or missing base unit reference'
FROM public.material_families f
LEFT JOIN public.units_of_measure u ON u.id = f.base_unit_id
WHERE f.base_unit_id IS NULL OR u.id IS NULL;

SELECT 'material_missing_or_invalid_base_unit' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|materials with a null or missing base unit reference'
FROM public.materials m
LEFT JOIN public.units_of_measure u ON u.id = m.base_unit_id
WHERE m.base_unit_id IS NULL OR u.id IS NULL;

SELECT 'conversion_base_unit_mismatch' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|material conversions whose to_base_unit_id differs from Material.base_unit_id'
FROM public.material_unit_conversions c
JOIN public.materials m ON m.id = c.material_id
WHERE c.to_base_unit_id IS DISTINCT FROM m.base_unit_id;

SELECT 'non_supplier_receiving_info' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|receiving_info rows whose receiving_type is not Supplier'
FROM public.receiving_info WHERE receiving_type <> 'Supplier';

SELECT 'document_invalid_row_version' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|warehouse_documents rows with non-positive row_version'
FROM public.warehouse_documents WHERE row_version <= 0;

SELECT 'posted_document_missing_metadata' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|posted/reversed documents missing posted metadata or signed attachment'
FROM public.warehouse_documents
WHERE document_status IN ('Posted', 'Reversed')
  AND (posted_by IS NULL OR posted_at_utc IS NULL OR signed_copy_attachment_id IS NULL);

SELECT 'in_progress_count_missing_membership' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) ||
       '|InProgress counts with SelectedMaterials scope and no material membership rows'
FROM public.inventory_counts c
WHERE c.status = 'InProgress'
  AND c.scope_type = 'SelectedMaterials'
  AND NOT EXISTS (SELECT 1 FROM public.inventory_count_scope_materials m WHERE m.count_id = c.id);

SELECT 'invalid_document_sequence_facts' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|document sequences with year before 2000 or negative last_sequence'
FROM public.document_sequences WHERE year < 2000 OR last_sequence < 0;

SELECT 'duplicate_document_sequence_keys' || '|' ||
       CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) ||
       '|duplicate site/document_type/year sequence keys'
FROM (SELECT site_id, document_type, year FROM public.document_sequences
      GROUP BY site_id, document_type, year HAVING count(*) > 1) duplicates;

COMMIT;
