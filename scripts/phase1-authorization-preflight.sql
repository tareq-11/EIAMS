-- Run before applying ImplementSingleHierarchicalScope in every environment.
-- The query is read-only and intentionally never chooses an assignment or owner automatically.

SELECT
    users.id AS user_id,
    users.email,
    count(assignment.id) AS assignment_count,
    coalesce(
        jsonb_agg(
            jsonb_build_object(
                'assignment_id', assignment.id,
                'role_id', assignment.role_id,
                'scope_type', assignment.scope_type,
                'scope_id', assignment.scope_id)
            ORDER BY assignment.id)
            FILTER (WHERE assignment.id IS NOT NULL),
        '[]'::jsonb) AS assignments
FROM public.users AS users
LEFT JOIN public.user_role_scopes AS assignment ON assignment.user_id = users.id
GROUP BY users.id, users.email
ORDER BY assignment_count DESC, users.email, users.id;

SELECT
    warehouse.id AS warehouse_id,
    warehouse.code,
    warehouse.site_id,
    count(unit.id) FILTER (WHERE unit.status = 'Active') AS active_owner_candidates,
    coalesce(
        jsonb_agg(
            jsonb_build_object('id', unit.id, 'name', unit.name, 'status', unit.status)
            ORDER BY unit.name, unit.id)
            FILTER (WHERE unit.id IS NOT NULL),
        '[]'::jsonb) AS organizational_units_in_site
FROM public.warehouses AS warehouse
LEFT JOIN public.organizational_units AS unit ON unit.site_id = warehouse.site_id
GROUP BY warehouse.id, warehouse.code, warehouse.site_id
ORDER BY warehouse.code, warehouse.id;

