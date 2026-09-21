\set ON_ERROR_STOP on

-- RBAC v1 Phase 4 preflight. This file is intentionally read-only: run it against
-- a disposable PostgreSQL restore or Testcontainer, never a production/Neon writer.
BEGIN TRANSACTION READ ONLY;

DO $$
DECLARE
    active_count integer;
    active_vocabulary text;
    legacy_permissions integer;
    dotted_permissions integer;
    mapping_pairs integer;
    legacy_scope_rows integer;
    dotted_scope_rows integer;
    target_grants integer;
    target_role_scopes integer;
    orphan_grants integer;
    orphan_assignments integer;
    unknown_codes integer;
    duplicate_mappings integer;
    phase1_migration text;
    phase2_migration text;
    phase3_migration text;
BEGIN
    SELECT count(*), max(v.active_vocabulary) FILTER (WHERE v.is_active)
      INTO active_count, active_vocabulary
      FROM public.authorization_policy_versions v
     WHERE v.is_active;
    IF active_count <> 1 OR active_vocabulary NOT IN ('legacy-colon', 'dotted-v1') THEN
        RAISE EXCEPTION 'STOP: expected one supported active authorization marker; count=%, vocabulary=%', active_count, active_vocabulary;
    END IF;

    SELECT count(*) INTO legacy_permissions FROM public.permissions
     WHERE code = ANY (ARRAY[
       'users:access','organizations:view','organizations:manage','sites:view','sites:manage',
       'org-units:view','org-units:manage','employees:view','employees:manage','roles:view',
       'roles:manage','units-of-measure:view','units-of-measure:manage','material-domains:manage',
       'material-categories:manage','material-families:manage','materials:view','materials:manage',
       'warehouses:view','warehouses:manage','inventory:view','assets:view','custody:view',
       'custody:manage','warehouse-capabilities:manage','warehouse-material-settings:manage',
       'warehouse-documents:view','warehouse-documents:create','warehouse-documents:edit',
       'warehouse-documents:submit','warehouse-documents:cancel','warehouse-documents:review',
       'warehouse-documents:reverse','inventory-counts:view','inventory-counts:plan',
       'inventory-counts:enter-actual','inventory-counts:review','audit-logs:view']);
    SELECT count(*) INTO dotted_permissions FROM public.permissions
     WHERE code = ANY (ARRAY[
       'asset.view','audit.view','custody.assign','organization.view','organization.manage',
       'admin.role.view','admin.role.manage','catalog.manage','catalog.view','warehouse.manage',
       'warehouse.view','inventory.view','document.view','document.create','document.update',
       'document.submit','document.post','document.reject','document.cancel','document.reverse',
       'count.view','count.plan','count.enter','count.complete','count.close',
       'admin.user.view','admin.user.manage','document.revise','report.view']);
    IF legacy_permissions <> 38 OR dotted_permissions <> 29 THEN
        RAISE EXCEPTION 'STOP: permission catalog mismatch; legacy=%, dotted=%', legacy_permissions, dotted_permissions;
    END IF;

    SELECT count(*) INTO mapping_pairs FROM public.permission_code_mappings WHERE mapping_version = 1;
    SELECT count(*) INTO duplicate_mappings FROM (
        SELECT old_code, new_code, mapping_version FROM public.permission_code_mappings
        GROUP BY old_code, new_code, mapping_version HAVING count(*) <> 1) duplicates;
    IF mapping_pairs <> 42 OR duplicate_mappings <> 0 THEN
        RAISE EXCEPTION 'STOP: mapping mismatch; pairs=%, duplicate groups=%', mapping_pairs, duplicate_mappings;
    END IF;

    IF EXISTS (
        WITH expected(old_code, new_code, mapping_version) AS (VALUES
          ('assets:view','asset.view',1),('audit-logs:view','audit.view',1),
          ('custody:view','asset.view',1),('custody:manage','custody.assign',1),
          ('organizations:view','organization.view',1),('sites:view','organization.view',1),
          ('org-units:view','organization.view',1),('employees:view','organization.view',1),
          ('organizations:manage','organization.manage',1),('sites:manage','organization.manage',1),
          ('org-units:manage','organization.manage',1),('employees:manage','organization.manage',1),
          ('roles:view','admin.role.view',1),('roles:manage','admin.role.manage',1),
          ('material-categories:manage','catalog.manage',1),('material-domains:manage','catalog.manage',1),
          ('material-families:manage','catalog.manage',1),('materials:manage','catalog.manage',1),
          ('units-of-measure:manage','catalog.manage',1),('materials:view','catalog.view',1),
          ('units-of-measure:view','catalog.view',1),('warehouse-capabilities:manage','warehouse.manage',1),
          ('warehouse-material-settings:manage','warehouse.manage',1),('warehouses:manage','warehouse.manage',1),
          ('warehouses:view','warehouse.view',1),('inventory:view','inventory.view',1),
          ('warehouse-documents:view','document.view',1),('warehouse-documents:create','document.create',1),
          ('warehouse-documents:edit','document.update',1),('warehouse-documents:submit','document.submit',1),
          ('warehouse-documents:cancel','document.cancel',1),('warehouse-documents:review','document.post',1),
          ('warehouse-documents:review','document.reject',1),('warehouse-documents:reverse','document.reverse',1),
          ('inventory-counts:view','count.view',1),('inventory-counts:plan','count.plan',1),
          ('inventory-counts:enter-actual','count.enter',1),('inventory-counts:review','count.plan',1),
          ('inventory-counts:review','count.complete',1),('inventory-counts:review','count.close',1),
          ('users:access','admin.user.view',1),('users:access','admin.user.manage',1)
        ), actual AS (
          SELECT old_code, new_code, mapping_version
            FROM public.permission_code_mappings WHERE mapping_version = 1
        ), differences AS (
          SELECT * FROM actual EXCEPT SELECT * FROM expected
          UNION ALL
          SELECT * FROM expected EXCEPT SELECT * FROM actual
        ) SELECT 1 FROM differences)
    THEN
        RAISE EXCEPTION 'STOP: exact v1 mapping set differs from approved 42-pair contract';
    END IF;

    SELECT count(*) INTO legacy_scope_rows
      FROM public.permission_allowed_scope_types s
      JOIN public.permissions p ON p.id = s.permission_id
     WHERE p.code LIKE '%:%';
    SELECT count(*) INTO dotted_scope_rows
      FROM public.permission_allowed_scope_types s
      JOIN public.permissions p ON p.id = s.permission_id
      WHERE p.code LIKE '%.%';
    IF legacy_scope_rows <> 77 OR dotted_scope_rows <> 56 THEN
        RAISE EXCEPTION 'STOP: scope matrix mismatch; legacy=%, dotted=%', legacy_scope_rows, dotted_scope_rows;
    END IF;

    SELECT count(*) INTO target_grants
      FROM public.role_permissions rp JOIN public.permissions p ON p.id = rp.permission_id
     WHERE rp.role_id IN (
        '00000000-0000-0000-0000-000000000001'::uuid,
        '00000000-0000-0000-0000-000000000002'::uuid,
        '00000000-0000-0000-0000-000000000003'::uuid,
        '00000000-0000-0000-0000-000000000004'::uuid)
       AND p.code LIKE '%.%';
    SELECT count(*) INTO target_role_scopes
      FROM public.role_allowed_scope_types ras JOIN public.roles r ON r.id = ras.role_id
      WHERE ras.role_id IN (
        '00000000-0000-0000-0000-000000000001'::uuid,
        '00000000-0000-0000-0000-000000000002'::uuid,
        '00000000-0000-0000-0000-000000000003'::uuid,
        '00000000-0000-0000-0000-000000000004'::uuid);
    IF target_grants <> 51 OR target_role_scopes <> 9 THEN
        RAISE EXCEPTION 'STOP: target role seed mismatch; grants=%, role scopes=%', target_grants, target_role_scopes;
    END IF;

    SELECT count(*) INTO orphan_grants FROM public.role_permissions rp
      LEFT JOIN public.roles r ON r.id = rp.role_id
      LEFT JOIN public.permissions p ON p.id = rp.permission_id
     WHERE r.id IS NULL OR p.id IS NULL;
    SELECT count(*) INTO orphan_assignments FROM public.user_role_scopes urs
      LEFT JOIN public.users u ON u.id = urs.user_id
      LEFT JOIN public.roles r ON r.id = urs.role_id
     WHERE u.id IS NULL OR r.id IS NULL;
    IF orphan_grants <> 0 OR orphan_assignments <> 0 THEN
        RAISE EXCEPTION 'STOP: orphan references; grants=%, assignments=%', orphan_grants, orphan_assignments;
    END IF;

    SELECT count(*) INTO unknown_codes FROM public.permissions
     WHERE code NOT IN (
       SELECT unnest(ARRAY[
         'users:access','organizations:view','organizations:manage','sites:view','sites:manage',
         'org-units:view','org-units:manage','employees:view','employees:manage','roles:view',
         'roles:manage','units-of-measure:view','units-of-measure:manage','material-domains:manage',
         'material-categories:manage','material-families:manage','materials:view','materials:manage',
         'warehouses:view','warehouses:manage','inventory:view','assets:view','custody:view',
         'custody:manage','warehouse-capabilities:manage','warehouse-material-settings:manage',
         'warehouse-documents:view','warehouse-documents:create','warehouse-documents:edit',
         'warehouse-documents:submit','warehouse-documents:cancel','warehouse-documents:review',
         'warehouse-documents:reverse','inventory-counts:view','inventory-counts:plan',
         'inventory-counts:enter-actual','inventory-counts:review','audit-logs:view',
         'asset.view','audit.view','custody.assign','organization.view','organization.manage',
         'admin.role.view','admin.role.manage','catalog.manage','catalog.view','warehouse.manage',
         'warehouse.view','inventory.view','document.view','document.create','document.update',
         'document.submit','document.post','document.reject','document.cancel','document.reverse',
         'count.view','count.plan','count.enter','count.complete','count.close',
         'admin.user.view','admin.user.manage','document.revise','report.view']));
    IF unknown_codes <> 0 THEN
        RAISE EXCEPTION 'STOP: unknown permission codes=%', unknown_codes;
    END IF;

    SELECT max(migration_id) FILTER (WHERE migration_id LIKE '%AddPermissionMappingAndPolicyVersion'),
           max(migration_id) FILTER (WHERE migration_id LIKE '%AddPermissionAllowedScopeTypes'),
           max(migration_id) FILTER (WHERE migration_id LIKE '%AddDottedAuthorizationCatalog')
      INTO phase1_migration, phase2_migration, phase3_migration
      FROM public."__EFMigrationsHistory";
    IF phase1_migration IS NULL OR phase2_migration IS NULL OR phase3_migration IS NULL
       OR phase1_migration >= phase2_migration OR phase2_migration >= phase3_migration THEN
        RAISE EXCEPTION 'STOP: Phase 1-3 migration history is incomplete or out of order';
    END IF;

    RAISE NOTICE 'RBAC v1 preflight passed: legacy=38, dotted=29, mappings=42, matrices=77+56, target grants=51';
END $$;

-- Exact dotted parity expectations. This is an audit report/check, not a grant derivation;
-- mapping rows are intentionally absent from the query.
DO $$
DECLARE
    expected_role text;
    expected_scope text;
    expected text[];
    actual text[];
BEGIN
    FOR expected_role, expected_scope IN
        SELECT * FROM (VALUES
            ('SYSTEM_ADMIN','Enterprise'),
            ('WH_MGR','Enterprise'), ('WH_MGR','Site'), ('WH_MGR','OrganizationalUnit'), ('WH_MGR','Warehouse'),
            ('WH_KEEPER','Warehouse'),
            ('AUDITOR','Enterprise'), ('AUDITOR','Site'), ('AUDITOR','Warehouse')) expected_rows
    LOOP
        expected := CASE
            WHEN expected_role = 'SYSTEM_ADMIN' THEN ARRAY['admin.role.manage','admin.role.view','admin.user.manage','admin.user.view','catalog.manage','catalog.view','organization.manage','organization.view','warehouse.manage','warehouse.view']
            WHEN expected_role = 'WH_MGR' AND expected_scope = 'Warehouse' THEN ARRAY['asset.view','catalog.view','count.close','count.complete','count.plan','count.view','document.cancel','document.create','document.post','document.reject','document.reverse','document.update','document.view','inventory.view','organization.view','report.view','warehouse.view']
            WHEN expected_role = 'WH_MGR' THEN ARRAY['asset.view','catalog.view','count.view','document.view','inventory.view','organization.view','report.view','warehouse.view']
            WHEN expected_role = 'WH_KEEPER' THEN ARRAY['asset.view','catalog.view','count.enter','count.view','custody.assign','document.cancel','document.create','document.revise','document.submit','document.update','document.view','inventory.view','organization.view','report.view','warehouse.view']
            ELSE ARRAY['asset.view','audit.view','catalog.view','count.view','document.view','inventory.view','organization.view','report.view','warehouse.view']
        END;
        SELECT COALESCE(array_agg(p.code ORDER BY p.code), ARRAY[]::text[]) INTO actual
          FROM public.roles r
          JOIN public.role_allowed_scope_types ras ON ras.role_id = r.id AND ras.scope_type = expected_scope
          JOIN public.role_permissions rp ON rp.role_id = r.id
          JOIN public.permissions p ON p.id = rp.permission_id
          JOIN public.permission_allowed_scope_types pas ON pas.permission_id = p.id AND pas.scope_type = ras.scope_type
         WHERE r.name = expected_role AND p.code LIKE '%.%';
        IF cardinality(actual) <> cardinality(expected) OR NOT (actual @> expected AND expected @> actual) THEN
            RAISE EXCEPTION 'STOP: dotted parity mismatch role=% scope=% expected=% actual=%', expected_role, expected_scope, expected, actual;
        END IF;
    END LOOP;
END $$;

-- Parity report: this is derived from role grants + role scope + permission scope only.
-- permission_code_mappings is deliberately not joined and cannot grant authorization.
\echo 'RBAC v1 effective parity report (legacy selected / dotted expected after cutover)'
SELECT r.name AS role_name,
       ras.scope_type,
       'legacy-colon' AS vocabulary,
       COALESCE(array_agg(p.code ORDER BY p.code) FILTER (WHERE p.code LIKE '%:%'), ARRAY[]::text[]) AS effective_codes
  FROM public.roles r
  JOIN public.role_allowed_scope_types ras ON ras.role_id = r.id
  LEFT JOIN public.role_permissions rp ON rp.role_id = r.id
  LEFT JOIN public.permissions p ON p.id = rp.permission_id
  JOIN public.permission_allowed_scope_types pas
    ON pas.permission_id = p.id AND pas.scope_type = ras.scope_type
 GROUP BY r.name, ras.scope_type
 UNION ALL
SELECT r.name,
       ras.scope_type,
       'dotted-v1',
       COALESCE(array_agg(p.code ORDER BY p.code) FILTER (WHERE p.code LIKE '%.%'), ARRAY[]::text[])
  FROM public.roles r
  JOIN public.role_allowed_scope_types ras ON ras.role_id = r.id
  LEFT JOIN public.role_permissions rp ON rp.role_id = r.id
  LEFT JOIN public.permissions p ON p.id = rp.permission_id
  JOIN public.permission_allowed_scope_types pas
    ON pas.permission_id = p.id AND pas.scope_type = ras.scope_type
 GROUP BY r.name, ras.scope_type
 ORDER BY role_name, scope_type, vocabulary;

ROLLBACK;
