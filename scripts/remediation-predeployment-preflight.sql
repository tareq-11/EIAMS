-- EIAMS remediation pre-deployment report.
-- Read-only: every result set must be reviewed before applying migrations in a target environment.

-- 1. Users without exactly one Role/Scope assignment.
SELECT
    u.id AS user_id,
    u.email,
    count(urs.id) AS assignment_count
FROM public.users AS u
LEFT JOIN public.user_role_scopes AS urs ON urs.user_id = u.id
GROUP BY u.id, u.email
HAVING count(urs.id) <> 1
ORDER BY assignment_count DESC, u.email;

-- 2. Warehouses without an administrative owner or linked to an owner from another Site.
SELECT
    w.id AS warehouse_id,
    w.code AS warehouse_code,
    w.site_id AS warehouse_site_id,
    w.organizational_unit_id,
    ou.site_id AS organizational_unit_site_id,
    CASE
        WHEN w.organizational_unit_id IS NULL THEN 'MISSING_OWNER'
        WHEN ou.id IS NULL THEN 'OWNER_NOT_FOUND'
        WHEN ou.site_id <> w.site_id THEN 'CROSS_SITE_OWNER'
    END AS issue
FROM public.warehouses AS w
LEFT JOIN public.organizational_units AS ou ON ou.id = w.organizational_unit_id
WHERE w.organizational_unit_id IS NULL
   OR ou.id IS NULL
   OR ou.site_id <> w.site_id
ORDER BY w.code, w.id;

-- 3. Materials without a valid base unit or with an invalid classification combination.
SELECT
    m.id AS material_id,
    m.code,
    m.base_unit_id,
    m.material_kind,
    m.tracking_type,
    m.requires_asset_number,
    CASE
        WHEN m.base_unit_id IS NULL THEN 'MISSING_BASE_UNIT'
        WHEN uom.id IS NULL THEN 'BASE_UNIT_NOT_FOUND'
        WHEN m.material_kind = 'Consumable'
             AND (m.tracking_type <> 'Quantity' OR m.requires_asset_number) THEN 'INVALID_CONSUMABLE'
        WHEN m.material_kind = 'Durable'
             AND m.requires_asset_number THEN 'INVALID_DURABLE'
        WHEN m.material_kind = 'Asset'
             AND (m.tracking_type <> 'Serial' OR NOT m.requires_asset_number) THEN 'INVALID_ASSET'
        WHEN m.material_kind NOT IN ('Consumable', 'Durable', 'Asset') THEN 'UNKNOWN_KIND'
        WHEN m.tracking_type NOT IN ('Quantity', 'Serial') THEN 'UNKNOWN_TRACKING'
    END AS issue
FROM public.materials AS m
LEFT JOIN public.units_of_measure AS uom ON uom.id = m.base_unit_id
WHERE m.base_unit_id IS NULL
   OR uom.id IS NULL
   OR (m.material_kind = 'Consumable' AND (m.tracking_type <> 'Quantity' OR m.requires_asset_number))
   OR (m.material_kind = 'Durable' AND m.requires_asset_number)
   OR (m.material_kind = 'Asset' AND (m.tracking_type <> 'Serial' OR NOT m.requires_asset_number))
   OR m.material_kind NOT IN ('Consumable', 'Durable', 'Asset')
   OR m.tracking_type NOT IN ('Quantity', 'Serial')
ORDER BY m.code, m.id;

-- 4. Unit conversions whose destination no longer matches the Material base unit.
SELECT
    muc.id AS conversion_id,
    muc.material_id,
    muc.from_unit_id,
    muc.to_base_unit_id,
    m.base_unit_id AS expected_base_unit_id,
    muc.factor
FROM public.material_unit_conversions AS muc
JOIN public.materials AS m ON m.id = muc.material_id
LEFT JOIN public.units_of_measure AS from_uom ON from_uom.id = muc.from_unit_id
LEFT JOIN public.units_of_measure AS to_uom ON to_uom.id = muc.to_base_unit_id
WHERE muc.to_base_unit_id <> m.base_unit_id
   OR muc.factor <= 0
   OR from_uom.id IS NULL
   OR to_uom.id IS NULL
ORDER BY muc.material_id, muc.id;

-- 5. Invalid Signed Original state or more than one active Signed Original per document.
SELECT
    da.document_id,
    count(*) FILTER (WHERE da.attachment_type = 'SignedOriginal' AND da.is_active) AS active_count,
    count(*) FILTER (
        WHERE da.attachment_type = 'SignedOriginal'
          AND ((da.is_active AND (da.archived_at_utc IS NOT NULL OR da.archived_by IS NOT NULL))
            OR (NOT da.is_active AND (da.archived_at_utc IS NULL OR da.archived_by IS NULL)))) AS invalid_state_count
FROM public.document_attachments AS da
WHERE da.attachment_type = 'SignedOriginal'
GROUP BY da.document_id
HAVING count(*) FILTER (WHERE da.attachment_type = 'SignedOriginal' AND da.is_active) > 1
    OR count(*) FILTER (
        WHERE da.attachment_type = 'SignedOriginal'
          AND ((da.is_active AND (da.archived_at_utc IS NOT NULL OR da.archived_by IS NOT NULL))
            OR (NOT da.is_active AND (da.archived_at_utc IS NULL OR da.archived_by IS NULL)))) > 0
ORDER BY da.document_id;
