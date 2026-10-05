using Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Code).HasMaxLength(100);

        builder.Property(p => p.NameAr).IsRequired().HasMaxLength(Permission.NameArMaxLength);

        builder.Property(p => p.DescriptionAr).HasMaxLength(Permission.DescriptionArMaxLength);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "ck_permissions_name_ar_not_blank",
                "length(btrim(name_ar)) > 0"));

        // The dotted v1 catalogue is the only live one. The legacy colon catalogue was deleted by
        // 20260922220000_CutoverToDottedOnlyPermissionVocabulary, whose Down migration states the rows
        // are intentionally not recreated; keeping them seeded here meant the model claimed 67 rows
        // while the database held 29, so any future InsertData against this table would have
        // resurrected codes the cutover deliberately removed. PermissionCodeMapping retains the
        // legacy-to-dotted history that audit and remediation rely on.
        //
        // NameAr and DescriptionAr are required by the Arabic-first role permission matrix
        // (docs/route-permission-scope-matrix.md: the catalogue must expose nameAr/descriptionAr).
        builder.HasData(
            new
            {
                Id = WellKnownDottedPermissions.AssetViewId,
                Code = "asset.view",
                NameAr = "عرض الأصول",
                DescriptionAr = "عرض سجل الأصول وحالتها الحالية.",
                Description = (string?)"View assets."
            },
            new
            {
                Id = WellKnownDottedPermissions.AuditViewId,
                Code = "audit.view",
                NameAr = "عرض سجل التدقيق",
                DescriptionAr = "عرض سجلات التدقيق وتفاصيل التغييرات على السجلات.",
                Description = (string?)"View audit history."
            },
            new
            {
                Id = WellKnownDottedPermissions.CustodyAssignId,
                Code = "custody.assign",
                NameAr = "إسناد الحيازة",
                DescriptionAr = "إسناد الأصول إلى حائزها وسحبها منه.",
                Description = (string?)"Assign personal custody."
            },
            new
            {
                Id = WellKnownDottedPermissions.OrganizationViewId,
                Code = "organization.view",
                NameAr = "عرض المنشآت",
                DescriptionAr = "عرض المنشآت والمواقع التابعة لها.",
                Description = (string?)"View organization structure."
            },
            new
            {
                Id = WellKnownDottedPermissions.OrganizationManageId,
                Code = "organization.manage",
                NameAr = "إدارة المنشآت",
                DescriptionAr = "إنشاء المنشآت وتعديل بياناتها وحالتها.",
                Description = (string?)"Manage organization structure."
            },
            new
            {
                Id = WellKnownDottedPermissions.AdminRoleViewId,
                Code = "admin.role.view",
                NameAr = "عرض الأدوار",
                DescriptionAr = "عرض الأدوار وصلاحياتها دون تعديل.",
                Description = (string?)"View roles and permissions."
            },
            new
            {
                Id = WellKnownDottedPermissions.AdminRoleManageId,
                Code = "admin.role.manage",
                NameAr = "إدارة الأدوار",
                DescriptionAr = "إنشاء الأدوار وتعديل بياناتها واستبدال صلاحياتها.",
                Description = (string?)"Manage roles and permissions."
            },
            new
            {
                Id = WellKnownDottedPermissions.CatalogManageId,
                Code = "catalog.manage",
                NameAr = "إدارة الفهرس",
                DescriptionAr = "إدارة النطاقات والفئات والعائلات والمواد ووحدات القياس.",
                Description = (string?)"Manage the material catalog."
            },
            new
            {
                Id = WellKnownDottedPermissions.CatalogViewId,
                Code = "catalog.view",
                NameAr = "عرض الفهرس",
                DescriptionAr = "عرض فهرس المواد والوحدات دون تعديل.",
                Description = (string?)"View the material catalog."
            },
            new
            {
                Id = WellKnownDottedPermissions.WarehouseManageId,
                Code = "warehouse.manage",
                NameAr = "إدارة المستودعات",
                DescriptionAr = "إنشاء المستودعات وتعديل بياناتها وقدراتها.",
                Description = (string?)"Manage warehouses and capabilities."
            },
            new
            {
                Id = WellKnownDottedPermissions.WarehouseViewId,
                Code = "warehouse.view",
                NameAr = "عرض المستودعات",
                DescriptionAr = "عرض المستودعات وقدراتها وإعدادات المواد فيها.",
                Description = (string?)"View warehouses and settings."
            },
            new
            {
                Id = WellKnownDottedPermissions.InventoryViewId,
                Code = "inventory.view",
                NameAr = "عرض المخزون",
                DescriptionAr = "عرض أرصدة المخزون وحركاته.",
                Description = (string?)"View inventory."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentViewId,
                Code = "document.view",
                NameAr = "عرض السندات",
                DescriptionAr = "عرض سندات المستودعات وتفاصيلها.",
                Description = (string?)"View warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentCreateId,
                Code = "document.create",
                NameAr = "إنشاء السند",
                DescriptionAr = "إنشاء مسودات سندات المستودعات وإضافة بنودها.",
                Description = (string?)"Create warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentUpdateId,
                Code = "document.update",
                NameAr = "تعديل السند",
                DescriptionAr = "تعديل بيانات السند قبل اعتماده.",
                Description = (string?)"Update warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentSubmitId,
                Code = "document.submit",
                NameAr = "إرسال السند",
                DescriptionAr = "إرسال السند للترحيل بعد اكتماله.",
                Description = (string?)"Submit warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentPostId,
                Code = "document.post",
                NameAr = "ترحيل السند",
                DescriptionAr = "ترحيل السند وتسجيل أثره على المخزون.",
                Description = (string?)"Post warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentRejectId,
                Code = "document.reject",
                NameAr = "رفض السند",
                DescriptionAr = "رفض السند وإعادته إلى المسودة.",
                Description = (string?)"Reject warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentCancelId,
                Code = "document.cancel",
                NameAr = "إلغاء السند",
                DescriptionAr = "إلغاء السند قبل اعتماده.",
                Description = (string?)"Cancel warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentReverseId,
                Code = "document.reverse",
                NameAr = "عكس السند",
                DescriptionAr = "عكس أثر سند تم ترحيله.",
                Description = (string?)"Reverse warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.DocumentReviseId,
                Code = "document.revise",
                NameAr = "مراجعة السند",
                DescriptionAr = "تعديل السند الذي طُلبت مراجعته.",
                Description = (string?)"Revise rejected warehouse documents."
            },
            new
            {
                Id = WellKnownDottedPermissions.CountViewId,
                Code = "count.view",
                NameAr = "عرض الجرد",
                DescriptionAr = "عرض الجرد الحالي وجلساته ونتائجه.",
                Description = (string?)"View inventory counts."
            },
            new
            {
                Id = WellKnownDottedPermissions.CountPlanId,
                Code = "count.plan",
                NameAr = "التخطيط للجرد",
                DescriptionAr = "إنشاء جلسة جرد وتحديد نطاقها.",
                Description = (string?)"Plan inventory counts."
            },
            new
            {
                Id = WellKnownDottedPermissions.CountEnterId,
                Code = "count.enter",
                NameAr = "إدخال الجرد",
                DescriptionAr = "إدخال الأعداد الفعلية أثناء الجرد.",
                Description = (string?)"Enter count actuals."
            },
            new
            {
                Id = WellKnownDottedPermissions.CountCompleteId,
                Code = "count.complete",
                NameAr = "إتمام الجرد",
                DescriptionAr = "اعتماد نتائج الجرد بعد إدخال جميع الأعداد الفعلية.",
                Description = (string?)"Complete inventory counts."
            },
            new
            {
                Id = WellKnownDottedPermissions.CountCloseId,
                Code = "count.close",
                NameAr = "إقفال الجرد",
                DescriptionAr = "إقفال جلسة الجرد واعتماد نتائجها.",
                Description = (string?)"Close inventory counts."
            },
            new
            {
                Id = WellKnownDottedPermissions.AdminUserViewId,
                Code = "admin.user.view",
                NameAr = "عرض المستخدمين",
                DescriptionAr = "عرض بيانات المستخدمين وإسنادياتهم دون تعديل.",
                Description = (string?)"View users."
            },
            new
            {
                Id = WellKnownDottedPermissions.AdminUserManageId,
                Code = "admin.user.manage",
                NameAr = "إدارة المستخدمين",
                DescriptionAr = "إنشاء المستخدمين وتعديل بياناتهم وإسناد أدوارهم ونطاقاتهم.",
                Description = (string?)"Manage users."
            },
            new
            {
                Id = WellKnownDottedPermissions.ReportViewId,
                Code = "report.view",
                NameAr = "عرض التقارير",
                DescriptionAr = "عرض التقارير ولوحات المؤشرات.",
                Description = (string?)"View operational reports."
            });
    }
}