using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class AddDottedAuthorizationCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000201', 'asset.view', 'View assets.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000202', 'audit.view', 'View audit history.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000203', 'custody.assign', 'Assign personal custody.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000204', 'organization.view', 'View organization structure.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000205', 'organization.manage', 'Manage organization structure.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000206', 'admin.role.view', 'View roles and permissions.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000207', 'admin.role.manage', 'Manage roles and permissions.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000208', 'catalog.manage', 'Manage the material catalog.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000209', 'catalog.view', 'View the material catalog.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000210', 'warehouse.manage', 'Manage warehouses and capabilities.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000211', 'warehouse.view', 'View warehouses and settings.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000212', 'inventory.view', 'View inventory.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000213', 'document.view', 'View warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000214', 'document.create', 'Create warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000215', 'document.update', 'Update warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000216', 'document.submit', 'Submit warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000217', 'document.post', 'Post warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000218', 'document.reject', 'Reject warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000219', 'document.cancel', 'Cancel warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000220', 'document.reverse', 'Reverse warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000221', 'count.view', 'View inventory counts.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000222', 'count.plan', 'Plan inventory counts.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000223', 'count.enter', 'Enter count actuals.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000224', 'count.complete', 'Complete inventory counts.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000225', 'count.close', 'Close inventory counts.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000226', 'admin.user.view', 'View users.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000227', 'admin.user.manage', 'Manage users.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000228', 'document.revise', 'Revise rejected warehouse documents.');
INSERT INTO public.permissions (id, code, description)
VALUES ('00000000-0000-0000-0000-000000000229', 'report.view', 'View operational reports.');

INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000003', 'Enterprise');
INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000003', 'Site');

UPDATE public.roles SET description = 'Enterprise structural administration. Automatically granted to the first registered user.', name = 'SYSTEM_ADMIN'
WHERE id = '00000000-0000-0000-0000-000000000001';

INSERT INTO public.roles (id, created_at_utc, created_by, description, name, updated_at_utc, updated_by)
VALUES ('00000000-0000-0000-0000-000000000004', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, 'Read-only audit and operational reporting role.', 'AUDITOR', NULL, NULL);

INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000201', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000201', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000201', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000201', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000202', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000202', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000202', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000202', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000203', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000204', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000204', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000204', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000204', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000205', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000206', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000207', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000208', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000209', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000209', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000209', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000209', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000210', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000211', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000211', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000211', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000211', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000212', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000212', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000212', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000212', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000213', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000213', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000213', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000213', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000214', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000215', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000216', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000217', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000218', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000219', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000220', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000221', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000221', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000221', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000221', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000222', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000223', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000224', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000225', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000226', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000227', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000228', 'Warehouse');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000229', 'Enterprise');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000229', 'OrganizationalUnit');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000229', 'Site');
INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000229', 'Warehouse');

INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000004', 'Enterprise');
INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000004', 'Site');
INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
VALUES ('00000000-0000-0000-0000-000000000004', 'Warehouse');

INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000205', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000206', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000207', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000208', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000209', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000210', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000211', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000226', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000227', '00000000-0000-0000-0000-000000000001');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000203', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000209', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000211', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000212', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000213', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000214', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000215', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000216', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000219', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000221', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000223', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000228', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000229', '00000000-0000-0000-0000-000000000002');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000209', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000211', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000212', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000213', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000214', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000215', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000217', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000218', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000219', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000220', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000221', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000222', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000224', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000225', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000229', '00000000-0000-0000-0000-000000000003');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000204', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000209', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000211', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000212', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000213', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000221', '00000000-0000-0000-0000-000000000004');
INSERT INTO public.role_permissions (permission_id, role_id)
VALUES ('00000000-0000-0000-0000-000000000229', '00000000-0000-0000-0000-000000000004');

""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000202' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000202' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000202' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000202' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000203' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000205' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000206' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000207' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000208' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000210' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000214' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000215' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000216' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000217' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000218' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000219' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000220' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000222' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000223' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000224' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000225' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000226' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000227' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000228' AND scope_type = 'Warehouse';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND scope_type = 'Enterprise';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND scope_type = 'OrganizationalUnit';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND scope_type = 'Site';

DELETE FROM public.permission_allowed_scope_types
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND scope_type = 'Warehouse';

DELETE FROM public.role_allowed_scope_types
WHERE role_id = '00000000-0000-0000-0000-000000000003' AND scope_type = 'Enterprise';

DELETE FROM public.role_allowed_scope_types
WHERE role_id = '00000000-0000-0000-0000-000000000003' AND scope_type = 'Site';

DELETE FROM public.role_allowed_scope_types
WHERE role_id = '00000000-0000-0000-0000-000000000004' AND scope_type = 'Enterprise';

DELETE FROM public.role_allowed_scope_types
WHERE role_id = '00000000-0000-0000-0000-000000000004' AND scope_type = 'Site';

DELETE FROM public.role_allowed_scope_types
WHERE role_id = '00000000-0000-0000-0000-000000000004' AND scope_type = 'Warehouse';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000205' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000206' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000207' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000208' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000210' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000226' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000227' AND role_id = '00000000-0000-0000-0000-000000000001';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000203' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000214' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000215' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000216' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000219' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000223' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000228' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND role_id = '00000000-0000-0000-0000-000000000002';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000214' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000215' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000217' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000218' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000219' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000220' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000222' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000224' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000225' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND role_id = '00000000-0000-0000-0000-000000000003';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000201' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000202' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000204' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000209' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000211' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000212' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000213' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000221' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.role_permissions
WHERE permission_id = '00000000-0000-0000-0000-000000000229' AND role_id = '00000000-0000-0000-0000-000000000004';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000201';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000202';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000203';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000204';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000205';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000206';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000207';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000208';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000209';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000210';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000211';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000212';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000213';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000214';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000215';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000216';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000217';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000218';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000219';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000220';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000221';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000222';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000223';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000224';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000225';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000226';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000227';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000228';

DELETE FROM public.permissions
WHERE id = '00000000-0000-0000-0000-000000000229';

DELETE FROM public.roles
WHERE id = '00000000-0000-0000-0000-000000000004';

UPDATE public.roles SET description = 'Full enterprise administrative access. Automatically granted to the first registered user.', name = 'Administrator'
WHERE id = '00000000-0000-0000-0000-000000000001';

""");
    }
}
