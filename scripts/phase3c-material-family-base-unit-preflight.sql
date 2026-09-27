-- Phase 3C read-only preflight. Run against the exact database that will receive
-- the migration, using a read-only database role if available.
BEGIN TRANSACTION READ ONLY;

WITH findings AS (
    SELECT 'MATERIAL_BASE_UNIT_INVALID'::text AS finding, count(*)::bigint AS affected_rows
    FROM public.materials AS m
    LEFT JOIN public.units_of_measure AS u ON u.id = m.base_unit_id
    WHERE m.base_unit_id IS NULL
       OR m.base_unit_id = '00000000-0000-0000-0000-000000000000'::uuid
       OR u.id IS NULL

    UNION ALL

    SELECT 'FAMILY_BASE_UNIT_INVALID', count(*)
    FROM public.material_families AS f
    LEFT JOIN public.units_of_measure AS u ON u.id = f.base_unit_id
    WHERE f.base_unit_id IS NULL
       OR f.base_unit_id = '00000000-0000-0000-0000-000000000000'::uuid
       OR u.id IS NULL

    UNION ALL

    SELECT 'FAMILY_WITH_NO_MATERIALS', count(*)
    FROM public.material_families AS f
    WHERE NOT EXISTS (SELECT 1 FROM public.materials AS m WHERE m.family_id = f.id)

    UNION ALL

    SELECT 'FAMILY_MATERIAL_UNIT_DISAGREEMENT', count(*)
    FROM public.material_families AS f
    WHERE EXISTS (
        SELECT 1 FROM public.materials AS m
        WHERE m.family_id = f.id AND m.base_unit_id <> f.base_unit_id
    )

    UNION ALL

    SELECT 'FAMILY_WITH_MULTIPLE_MATERIAL_BASE_UNITS', count(*)
    FROM (
        SELECT m.family_id
        FROM public.materials AS m
        GROUP BY m.family_id
        HAVING count(DISTINCT m.base_unit_id) > 1
    ) AS mixed_families

    UNION ALL

    SELECT 'MATERIAL_CONVERSION_PROVENANCE_INVALID', count(*)
    FROM public.material_unit_conversions AS c
    LEFT JOIN public.materials AS m ON m.id = c.material_id
    LEFT JOIN public.units_of_measure AS source_unit ON source_unit.id = c.from_unit_id
    LEFT JOIN public.units_of_measure AS target_unit ON target_unit.id = c.to_base_unit_id
    WHERE m.id IS NULL
       OR source_unit.id IS NULL
       OR target_unit.id IS NULL
       OR c.from_unit_id = c.to_base_unit_id
       OR c.to_base_unit_id <> m.base_unit_id

    UNION ALL

    SELECT 'DOCUMENT_LINE_PROVENANCE_PARTIAL', count(*)
    FROM public.document_lines AS l
    WHERE (l.source_material_version IS NULL AND (
               l.source_material_kind IS NOT NULL
            OR l.source_tracking_type IS NOT NULL
            OR l.source_base_unit_id IS NOT NULL
        ))
       OR (l.source_material_version IS NOT NULL AND (
               l.source_material_kind IS NULL
            OR l.source_tracking_type IS NULL
            OR l.source_base_unit_id IS NULL
        ))
)
SELECT finding, affected_rows
FROM findings
ORDER BY finding;

-- Diagnostic only: shows family/material disagreement counts without exposing IDs.
-- Families may contain materials with different base units after this decision;
-- do not copy a family unit onto materials. Resolve existing discrepancies by
-- verifying Material.BaseUnitId and document-line provenance before cutover.
ROLLBACK;
