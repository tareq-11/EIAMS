using Application.Abstractions.Audit;
using Application.AuditLogs;

namespace Infrastructure.AuditLogs;

internal sealed class AuditRedactionService : IAuditRedactionService
{
    private static readonly Dictionary<string, (string Ar, string En)> ActionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Create"] = ("إنشاء", "Create"),
        ["Created"] = ("تم الإنشاء", "Created"),
        ["Update"] = ("تعديل", "Update"),
        ["Updated"] = ("تم التعديل", "Updated"),
        ["Delete"] = ("حذف", "Delete"),
        ["Deleted"] = ("تم الحذف", "Deleted"),
        ["Submit"] = ("إرسال للتدقيق", "Submit"),
        ["Submitted"] = ("تم الإرسال للتدقيق", "Submitted"),
        ["Review"] = ("مراجعة", "Review"),
        ["Reviewed"] = ("تمت المراجعة", "Reviewed"),
        ["Post"] = ("ترحيل", "Post"),
        ["Posted"] = ("تم الترحيل", "Posted"),
        ["Cancel"] = ("إلغاء", "Cancel"),
        ["Cancelled"] = ("تم الإلغاء", "Cancelled"),
        ["Reject"] = ("رفض", "Reject"),
        ["Rejected"] = ("تم الرفض", "Rejected"),
        ["ReturnToDraft"] = ("إعادة للمسودة", "Return to Draft"),
        ["ReturnedToDraft"] = ("تمت الإعادة للمسودة", "Returned to Draft"),
        ["Reverse"] = ("عكس", "Reverse"),
        ["Reversed"] = ("تم العكس", "Reversed"),
        ["Transfer"] = ("نقل", "Transfer"),
        ["Transferred"] = ("تم النقل", "Transferred"),
        ["Assign"] = ("تسليم عهدة", "Assign Custody"),
        ["Assigned"] = ("تم تسليم العهدة", "Assigned Custody"),
        ["Return"] = ("إرجاع", "Return"),
        ["Returned"] = ("تم الإرجاع", "Returned"),
        ["Close"] = ("إغلاق", "Close"),
        ["Closed"] = ("تم الإغلاق", "Closed"),
        ["Record"] = ("تسجيل", "Record"),
        ["Recorded"] = ("تم التسجيل", "Recorded"),
        ["SetStatus"] = ("تغيير الحالة", "Change Status"),
        ["SetSignedCopy"] = ("إرفاق الأصل الموقع", "Attach Signed Copy"),
        ["Archive"] = ("أرشفة", "Archive"),
        ["Archived"] = ("تمت الأرشفة", "Archived"),
        ["Authenticate"] = ("تسجيل دخول", "Authenticate"),
        ["TokenRefresh"] = ("تجديد الجلسة", "Token Refresh"),
        ["RevokeTokens"] = ("إلغاء الجلسات", "Revoke Tokens"),
        ["Logout"] = ("تسجيل خروج", "Logout")
    };

    private static readonly Dictionary<string, (string Ar, string En)> EntityTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["User"] = ("مستخدم", "User"),
        ["Role"] = ("دور", "Role"),
        ["RolePermission"] = ("صلاحية دور", "Role Permission"),
        ["RoleAllowedScopeType"] = ("نوع نطاق مسموح للدور", "Role Allowed Scope Type"),
        ["UserRoleScope"] = ("نطاق دور المستخدم", "User Role Scope"),
        ["Organization"] = ("مؤسسة", "Organization"),
        ["Site"] = ("موقع", "Site"),
        ["OrganizationalUnit"] = ("وحدة تنظيمية", "Organizational Unit"),
        ["Employee"] = ("موظف", "Employee"),
        ["ExternalParty"] = ("جهة خارجية", "External Party"),
        ["UnitOfMeasure"] = ("وحدة قياس", "Unit of Measure"),
        ["MaterialDomain"] = ("مجال مواد", "Material Domain"),
        ["MaterialCategory"] = ("فئة مواد", "Material Category"),
        ["MaterialFamily"] = ("عائلة مواد", "Material Family"),
        ["Material"] = ("مادة", "Material"),
        ["MaterialUnitConversion"] = ("معامل تحويل وحدة", "Unit Conversion"),
        ["Warehouse"] = ("مستودع", "Warehouse"),
        ["WarehouseCapability"] = ("قدرة مستودع", "Warehouse Capability"),
        ["WarehouseCapabilityOperation"] = ("عملية قدرة مستودع", "Warehouse Capability Operation"),
        ["WarehouseMaterialSetting"] = ("إعداد مادة في مستودع", "Warehouse Material Setting"),
        ["WarehouseDocument"] = ("مستند مستودعي", "Warehouse Document"),
        ["DocumentLine"] = ("بند مستند", "Document Line"),
        ["DocumentAttachment"] = ("مرفق مستند", "Document Attachment"),
        ["StockMovement"] = ("حركة مخزنية", "Stock Movement"),
        ["InventoryBalance"] = ("رصيد مخزني", "Inventory Balance"),
        ["Asset"] = ("أصل ثابت", "Asset"),
        ["AssetMovementHistory"] = ("سجل حركة أصل", "Asset Movement History"),
        ["Custody"] = ("عهدة أصل", "Asset Custody"),
        ["CustodyHistory"] = ("سجل حركة عهدة", "Custody History"),
        ["DocumentLineAssetSelection"] = ("تحديد أصل لبند", "Line Asset Selection"),
        ["ReceivingInfo"] = ("بيانات استلام", "Receiving Info"),
        ["IssueTo"] = ("بيانات جهة الصرف", "Issue To Info"),
        ["TransferInfo"] = ("بيانات نقل", "Transfer Info"),
        ["ReturnInfo"] = ("بيانات إرجاع", "Return Info"),
        ["InventoryCount"] = ("عملية جرد", "Inventory Count"),
        ["InventoryCountLine"] = ("بند جرد", "Inventory Count Line"),
        ["InventoryCountScopeMaterial"] = ("نطاق مادة في الجرد", "Inventory Count Material Scope"),
        ["InventoryAdjustment"] = ("تسوية مخزنية", "Inventory Adjustment"),
        ["AdjustmentLine"] = ("بند تسوية", "Adjustment Line"),
        ["TrackedMaterialUnit"] = ("وحدة مادة متتبعة (معمرة)", "Tracked Material Unit"),
        ["DurableCustodyAllocation"] = ("تخصيص عهدة معمرة (كمية)", "Durable Custody Allocation"),
        ["DurableCustodyHistory"] = ("سجل عهدة معمرة", "Durable Custody History"),
        ["DurableCustody"] = ("عهدة معمرة", "Durable Custody")
    };

    private static readonly Dictionary<string, (string Ar, string En)> FieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = ("الاسم", "Name"),
        ["name_ar"] = ("الاسم بالعربية", "Arabic Name"),
        ["name_en"] = ("الاسم بالإنجليزية", "English Name"),
        ["code"] = ("الرمز", "Code"),
        ["status"] = ("الحالة", "Status"),
        ["description"] = ("الوصف", "Description"),
        ["quantity"] = ("الكمية", "Quantity"),
        ["active_quantity"] = ("الكمية النشطة", "Active Quantity"),
        ["returned_quantity"] = ("الكمية المرجعة", "Returned Quantity"),
        ["issued_quantity"] = ("الكمية المصروفة", "Issued Quantity"),
        ["serial_number"] = ("الرقم التسلسلي", "Serial Number"),
        ["asset_number"] = ("رقم الأصل", "Asset Number"),
        ["holder_type"] = ("نوع الحائز", "Holder Type"),
        ["holder_id"] = ("معرف الحائز", "Holder ID"),
        ["custody_kind"] = ("نوع العهدة", "Custody Kind"),
        ["document_id"] = ("معرف المستند", "Document ID"),
        ["warehouse_id"] = ("معرف المستودع", "Warehouse ID"),
        ["material_id"] = ("معرف المادة", "Material ID"),
        ["employee_id"] = ("معرف الموظف", "Employee ID"),
        ["site_id"] = ("معرف الموقع", "Site ID"),
        ["email"] = ("البريد الإلكتروني", "Email"),
        ["first_name"] = ("الاسم الأول", "First Name"),
        ["last_name"] = ("اسم العائلة", "Last Name"),
        ["role_id"] = ("معرف الدور", "Role ID"),
        ["scope_type"] = ("نوع النطاق", "Scope Type"),
        ["scope_id"] = ("معرف النطاق", "Scope ID"),
        ["is_active"] = ("نشط", "Is Active"),
        ["note"] = ("ملاحظة", "Note"),
        ["notes"] = ("ملاحظات", "Notes")
    };

    public bool IsRedactedField(string entityType, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        return AuditSensitiveDataPolicy.IsSensitive(fieldName);
    }

    public string? GetRedactionReason(string entityType, string fieldName)
    {
        return AuditSensitiveDataPolicy.GetReason(fieldName);
    }

    public AuditLogEntryResponse RedactEntry(string entityType, AuditLogEntryResponse entry)
    {
        bool isRedacted = IsRedactedField(entityType, entry.FieldName);
        string? redactionReason = isRedacted ? GetRedactionReason(entityType, entry.FieldName) : null;

        string? oldValue = isRedacted ? null : entry.OldValue;
        string? newValue = isRedacted ? null : entry.NewValue;

        string fieldDisplayAr = GetFieldDisplayAr(entityType, entry.FieldName);
        string fieldDisplayEn = GetFieldDisplayEn(entityType, entry.FieldName);

        return entry with
        {
            OldValue = oldValue,
            NewValue = newValue,
            IsRedacted = isRedacted,
            RedactionReason = redactionReason,
            FieldDisplayAr = fieldDisplayAr,
            FieldDisplayEn = fieldDisplayEn
        };
    }

    public IReadOnlyList<AuditLogEntryResponse> RedactEntries(string entityType, IReadOnlyList<AuditLogEntryResponse> entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return Array.Empty<AuditLogEntryResponse>();
        }

        return entries.Select(entry => RedactEntry(entityType, entry)).ToList();
    }

    public bool IsSummaryRedacted(string? summary) =>
        AuditSensitiveDataPolicy.ContainsSensitiveJsonProperty(summary);

    public string GetActionDisplayAr(string action) =>
        ActionLabels.TryGetValue(action, out (string Ar, string En) label) ? label.Ar : action;

    public string GetActionDisplayEn(string action) =>
        ActionLabels.TryGetValue(action, out (string Ar, string En) label) ? label.En : action;

    public string GetEntityTypeDisplayAr(string entityType) =>
        EntityTypeLabels.TryGetValue(entityType, out (string Ar, string En) label) ? label.Ar : entityType;

    public string GetEntityTypeDisplayEn(string entityType) =>
        EntityTypeLabels.TryGetValue(entityType, out (string Ar, string En) label) ? label.En : entityType;

    public string GetFieldDisplayAr(string entityType, string fieldName) =>
        FieldLabels.TryGetValue(fieldName, out (string Ar, string En) label) ? label.Ar : fieldName;

    public string GetFieldDisplayEn(string entityType, string fieldName) =>
        FieldLabels.TryGetValue(fieldName, out (string Ar, string En) label) ? label.En : fieldName;
}
