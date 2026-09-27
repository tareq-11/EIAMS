using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <summary>
/// Removes the legacy permission catalog after the dotted-v1 marker and mapping contract have
/// been verified. Mapping rows remain append-only audit history; they never grant permissions.
/// </summary>
public partial class CutoverToDottedOnlyPermissionVocabulary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF (SELECT count(*) FROM public.authorization_policy_versions WHERE is_active) <> 1
                   OR (SELECT active_vocabulary FROM public.authorization_policy_versions WHERE is_active) <> 'dotted-v1' THEN
                    RAISE EXCEPTION '2A cutover blocked: exactly one active dotted-v1 policy marker is required';
                END IF;

                IF EXISTS (
                    SELECT 1 FROM public.permissions
                    WHERE code NOT IN (
                        'asset.view','audit.view','custody.assign','organization.view','organization.manage',
                        'admin.role.view','admin.role.manage','catalog.manage','catalog.view','warehouse.manage',
                        'warehouse.view','inventory.view','document.view','document.create','document.update',
                        'document.submit','document.post','document.reject','document.cancel','document.reverse',
                        'count.view','count.plan','count.enter','count.complete','count.close','admin.user.view',
                        'admin.user.manage','document.revise','report.view',
                        'users:access','organizations:view','organizations:manage','sites:view','sites:manage',
                        'org-units:view','org-units:manage','employees:view','employees:manage','roles:view',
                        'roles:manage','units-of-measure:view','units-of-measure:manage','material-domains:manage',
                        'material-categories:manage','material-families:manage','materials:view','materials:manage',
                        'warehouses:view','warehouses:manage','inventory:view','assets:view','custody:view',
                        'custody:manage','warehouse-capabilities:manage','warehouse-material-settings:manage',
                        'warehouse-documents:view','warehouse-documents:create','warehouse-documents:edit',
                        'warehouse-documents:submit','warehouse-documents:cancel','warehouse-documents:review',
                        'warehouse-documents:reverse','inventory-counts:view','inventory-counts:plan',
                        'inventory-counts:enter-actual','inventory-counts:review','audit-logs:view')) THEN
                    RAISE EXCEPTION '2A cutover blocked: unknown permission code exists';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public.permissions p
                    WHERE p.code LIKE '%:%'
                      AND NOT EXISTS (
                        SELECT 1 FROM public.permission_code_mappings m
                        WHERE m.mapping_version = 1
                          AND m.old_code = p.code
                          AND m.new_code LIKE '%.%')) THEN
                    RAISE EXCEPTION '2A cutover blocked: a legacy permission has no approved dotted mapping';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public.permission_code_mappings m
                    WHERE m.mapping_version = 1
                      AND (m.new_code NOT IN (
                          'asset.view','audit.view','custody.assign','organization.view','organization.manage',
                          'admin.role.view','admin.role.manage','catalog.manage','catalog.view','warehouse.manage',
                          'warehouse.view','inventory.view','document.view','document.create','document.update',
                          'document.submit','document.post','document.reject','document.cancel','document.reverse',
                          'count.view','count.plan','count.enter','count.complete','count.close','admin.user.view',
                          'admin.user.manage','document.revise','report.view')
                        OR NOT EXISTS (SELECT 1 FROM public.permissions p WHERE p.code = m.new_code))) THEN
                    RAISE EXCEPTION '2A cutover blocked: mapping target is unknown or non-dotted';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public.permission_code_mappings m
                    WHERE m.mapping_version = 1
                      AND m.old_code NOT IN (
                        'users:access','organizations:view','organizations:manage','sites:view','sites:manage',
                        'org-units:view','org-units:manage','employees:view','employees:manage','roles:view',
                        'roles:manage','units-of-measure:view','units-of-measure:manage','material-domains:manage',
                        'material-categories:manage','material-families:manage','materials:view','materials:manage',
                        'warehouses:view','warehouses:manage','inventory:view','assets:view','custody:view',
                        'custody:manage','warehouse-capabilities:manage','warehouse-material-settings:manage',
                        'warehouse-documents:view','warehouse-documents:create','warehouse-documents:edit',
                        'warehouse-documents:submit','warehouse-documents:cancel','warehouse-documents:review',
                        'warehouse-documents:reverse','inventory-counts:view','inventory-counts:plan',
                        'inventory-counts:enter-actual','inventory-counts:review','audit-logs:view')) THEN
                    RAISE EXCEPTION '2A cutover blocked: mapping source is unknown or ambiguous';
                END IF;

                IF EXISTS (
                    WITH expected(role_id, codes) AS (
                        VALUES
                            ('00000000-0000-0000-0000-000000000001'::uuid, ARRAY['catalog.view','catalog.manage','organization.view','organization.manage','warehouse.view','warehouse.manage','admin.user.view','admin.user.manage','admin.role.view','admin.role.manage']),
                            ('00000000-0000-0000-0000-000000000002'::uuid, ARRAY['catalog.view','organization.view','warehouse.view','inventory.view','document.view','document.create','document.update','document.submit','document.revise','document.cancel','count.view','count.enter','asset.view','custody.assign','report.view']),
                            ('00000000-0000-0000-0000-000000000003'::uuid, ARRAY['catalog.view','organization.view','warehouse.view','inventory.view','document.view','count.view','asset.view','report.view','document.create','document.update','document.post','document.reject','document.cancel','document.reverse','count.plan','count.complete','count.close']),
                            ('00000000-0000-0000-0000-000000000004'::uuid, ARRAY['catalog.view','organization.view','warehouse.view','inventory.view','document.view','count.view','asset.view','audit.view','report.view'])
                    ), expected_pairs AS (
                        SELECT role_id, code FROM expected CROSS JOIN LATERAL unnest(codes) AS code
                    ), actual_pairs AS (
                        SELECT rp.role_id, p.code FROM public.role_permissions rp JOIN public.permissions p ON p.id = rp.permission_id
                        WHERE p.code LIKE '%.%' AND rp.role_id IN (SELECT role_id FROM expected)
                    ), drift AS (
                        (SELECT role_id, code FROM expected_pairs EXCEPT SELECT role_id, code FROM actual_pairs)
                        UNION
                        (SELECT role_id, code FROM actual_pairs EXCEPT SELECT role_id, code FROM expected_pairs)
                    )
                    SELECT 1 FROM drift
                ) THEN
                    RAISE EXCEPTION '2A cutover blocked: seeded dotted role grant policy drift';
                END IF;

                IF EXISTS (
                    WITH expected(role_id, scope_types) AS (
                        VALUES
                            ('00000000-0000-0000-0000-000000000001'::uuid, ARRAY['Enterprise']),
                            ('00000000-0000-0000-0000-000000000002'::uuid, ARRAY['Warehouse']),
                            ('00000000-0000-0000-0000-000000000003'::uuid, ARRAY['Enterprise','Site','Warehouse']),
                            ('00000000-0000-0000-0000-000000000004'::uuid, ARRAY['Enterprise','Site','Warehouse'])
                    ), expected_pairs AS (
                        SELECT role_id, scope_type FROM expected CROSS JOIN LATERAL unnest(scope_types) AS scope_type
                    ), actual_pairs AS (
                        SELECT role_id, scope_type::text FROM public.role_allowed_scope_types WHERE role_id IN (SELECT role_id FROM expected)
                    ), drift AS (
                        (SELECT role_id, scope_type FROM expected_pairs EXCEPT SELECT role_id, scope_type FROM actual_pairs)
                        UNION
                        (SELECT role_id, scope_type FROM actual_pairs EXCEPT SELECT role_id, scope_type FROM expected_pairs)
                    )
                    SELECT 1 FROM drift
                ) THEN
                    RAISE EXCEPTION '2A cutover blocked: seeded role scope policy drift';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public.permissions p
                    JOIN public.role_permissions rp ON rp.permission_id = p.id
                    WHERE p.code LIKE '%:%'
                      AND rp.role_id <> '00000000-0000-0000-0000-000000000001'
                      -- WH_MGR intentionally moves count entry to the keeper role in dotted-v1.
                      AND NOT (rp.role_id = '00000000-0000-0000-0000-000000000003' AND p.code = 'inventory-counts:enter-actual')
                      AND EXISTS (
                          SELECT 1
                          FROM public.permission_code_mappings m
                          WHERE m.mapping_version = 1
                            AND m.old_code = p.code
                            AND NOT EXISTS (
                                SELECT 1
                                FROM public.role_permissions mapped_rp
                                JOIN public.permissions mapped_p ON mapped_p.id = mapped_rp.permission_id
                                WHERE mapped_rp.role_id = rp.role_id AND mapped_p.code = m.new_code))
                ) THEN
                    RAISE EXCEPTION '2A cutover blocked: legacy role grant parity is incomplete for role % and permission %',
                        (SELECT rp.role_id
                         FROM public.permissions p
                         JOIN public.role_permissions rp ON rp.permission_id = p.id
                         WHERE p.code LIKE '%:%'
                           AND rp.role_id <> '00000000-0000-0000-0000-000000000001'
                           AND NOT (rp.role_id = '00000000-0000-0000-0000-000000000003' AND p.code = 'inventory-counts:enter-actual')
                           AND EXISTS (
                               SELECT 1
                               FROM public.permission_code_mappings m
                               WHERE m.mapping_version = 1
                                 AND m.old_code = p.code
                                 AND NOT EXISTS (
                                     SELECT 1
                                     FROM public.role_permissions mapped_rp
                                     JOIN public.permissions mapped_p ON mapped_p.id = mapped_rp.permission_id
                                     WHERE mapped_rp.role_id = rp.role_id AND mapped_p.code = m.new_code))
                         LIMIT 1),
                        (SELECT p.code
                         FROM public.permissions p
                         JOIN public.role_permissions rp ON rp.permission_id = p.id
                         WHERE p.code LIKE '%:%'
                           AND rp.role_id <> '00000000-0000-0000-0000-000000000001'
                           AND NOT (rp.role_id = '00000000-0000-0000-0000-000000000003' AND p.code = 'inventory-counts:enter-actual')
                           AND EXISTS (
                               SELECT 1
                               FROM public.permission_code_mappings m
                               WHERE m.mapping_version = 1
                                 AND m.old_code = p.code
                                 AND NOT EXISTS (
                                     SELECT 1
                                     FROM public.role_permissions mapped_rp
                                     JOIN public.permissions mapped_p ON mapped_p.id = mapped_rp.permission_id
                                     WHERE mapped_rp.role_id = rp.role_id AND mapped_p.code = m.new_code))
                         LIMIT 1);
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM public.permission_allowed_scope_types pas
                    JOIN public.permissions p ON p.id = pas.permission_id
                    WHERE p.code LIKE '%:%'
                      AND EXISTS (
                          SELECT 1
                          FROM public.permission_code_mappings m
                          WHERE m.mapping_version = 1
                            AND m.old_code = p.code
                            AND NOT EXISTS (
                                SELECT 1
                                FROM public.permissions mapped_p
                                JOIN public.permission_allowed_scope_types mapped_pas ON mapped_pas.permission_id = mapped_p.id
                                WHERE mapped_p.code = m.new_code AND mapped_pas.scope_type = pas.scope_type))
                ) THEN
                    RAISE EXCEPTION '2A cutover blocked: legacy allowed-scope parity is incomplete';
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DELETE FROM public.role_permissions rp
            USING public.permissions p
            WHERE p.id = rp.permission_id AND p.code LIKE '%:%';

            DELETE FROM public.permission_allowed_scope_types pas
            USING public.permissions p
            WHERE p.id = pas.permission_id AND p.code LIKE '%:%';

            DELETE FROM public.permissions
            WHERE code LIKE '%:%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy permission rows and grants are intentionally not recreated. Restore requires
        // the approved backup; mapping history remains available for audit and remediation.
    }
}
