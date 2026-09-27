\set ON_ERROR_STOP on
BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '10000ms';
SET LOCAL lock_timeout = '2000ms';
\pset tuples_only on
\pset format unaligned
\pset fieldsep '|'
SELECT 'database|PASS|1|read-only transaction established';
SELECT 'organizational_unit_user_assignments' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) || '|user assignments requiring manual remediation' FROM public.user_role_scopes WHERE scope_type = 'OrganizationalUnit';
SELECT 'organizational_unit_role_allowed_scopes' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) || '|planned migration-managed removal of role allowed-scope rows' FROM public.role_allowed_scope_types WHERE scope_type = 'OrganizationalUnit';
SELECT 'organizational_unit_permission_allowed_scopes' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END || '|' || count(*) || '|planned migration-managed removal of permission allowed-scope rows' FROM public.permission_allowed_scope_types WHERE scope_type = 'OrganizationalUnit';
SELECT 'users_without_exactly_one_assignment' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) || '|active users with zero or any users with multiple assignments (suspended users may have zero)' FROM (SELECT u.id FROM public.users u LEFT JOIN public.user_role_scopes s ON s.user_id = u.id GROUP BY u.id, u.status HAVING count(s.id) > 1 OR (u.status = 'Active' AND count(s.id) = 0)) invalid_users;
SELECT 'invalid_assignment_scope_id' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) || '|assignment scope id does not match the three-value scope contract' FROM public.user_role_scopes s WHERE (s.scope_type = 'Enterprise' AND s.scope_id IS NOT NULL) OR (s.scope_type IN ('Site', 'Warehouse') AND s.scope_id IS NULL) OR (s.scope_type = 'Site' AND NOT EXISTS (SELECT 1 FROM public.sites x WHERE x.id = s.scope_id)) OR (s.scope_type = 'Warehouse' AND NOT EXISTS (SELECT 1 FROM public.warehouses x WHERE x.id = s.scope_id)) OR s.scope_type NOT IN ('Enterprise', 'Site', 'Warehouse');
SELECT 'role_scope_incompatibility' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END || '|' || count(*) || '|assignment role does not allow its scope type' FROM public.user_role_scopes s LEFT JOIN public.role_allowed_scope_types a ON a.role_id = s.role_id AND a.scope_type = s.scope_type WHERE a.role_id IS NULL;
COMMIT;
