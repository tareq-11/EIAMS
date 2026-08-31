using Application.AuditLogs;
using Infrastructure.AuditLogs;
using Shouldly;
using Xunit;

namespace Application.UnitTests.AuditLogs;

public sealed class AuditRedactionTests
{
    private readonly AuditRedactionService _service = new();

    [Theory]
    [InlineData("User", "password")]
    [InlineData("User", "password_hash")]
    [InlineData("User", "passwordhash")]
    [InlineData("RefreshToken", "token")]
    [InlineData("DocumentAttachment", "storage_key")]
    [InlineData("DocumentAttachment", "storagekey")]
    [InlineData("SystemConfig", "secret")]
    [InlineData("SystemConfig", "api_key")]
    [InlineData("SystemConfig", "connection_string")]
    public void IsRedactedField_Should_ReturnTrue_ForSensitiveFields(string entityType, string fieldName)
    {
        bool isRedacted = _service.IsRedactedField(entityType, fieldName);

        isRedacted.ShouldBeTrue();
    }

    [Theory]
    [InlineData("User", "email")]
    [InlineData("Material", "name_ar")]
    [InlineData("WarehouseDocument", "status")]
    [InlineData("StockMovement", "quantity")]
    [InlineData("Custody", "holder_id")]
    public void IsRedactedField_Should_ReturnFalse_ForNonSensitiveFields(string entityType, string fieldName)
    {
        bool isRedacted = _service.IsRedactedField(entityType, fieldName);

        isRedacted.ShouldBeFalse();
    }

    [Fact]
    public void RedactEntry_Should_MaskValuesAndSetRedactionMetadata_WhenFieldIsSensitive()
    {
        var entry = new AuditLogEntryResponse(
            Guid.NewGuid(),
            "password_hash",
            "AQAAAAIAAA...",
            "AQAAAAIBBB...");

        AuditLogEntryResponse result = _service.RedactEntry("User", entry);

        result.IsRedacted.ShouldBeTrue();
        result.RedactionReason.ShouldBe("CONFIDENTIAL_CREDENTIAL");
        result.OldValue.ShouldBeNull();
        result.NewValue.ShouldBeNull();
    }

    [Fact]
    public void RedactEntry_Should_PreserveValuesAndAddDisplayLabels_WhenFieldIsNotSensitive()
    {
        var entry = new AuditLogEntryResponse(
            Guid.NewGuid(),
            "status",
            "Draft",
            "Submitted");

        AuditLogEntryResponse result = _service.RedactEntry("WarehouseDocument", entry);

        result.IsRedacted.ShouldBeFalse();
        result.RedactionReason.ShouldBeNull();
        result.OldValue.ShouldBe("Draft");
        result.NewValue.ShouldBe("Submitted");
        result.FieldDisplayAr.ShouldBe("الحالة");
        result.FieldDisplayEn.ShouldBe("Status");
    }

    [Fact]
    public void IsSummaryRedacted_Should_ReturnTrue_WhenNestedSummaryContainsSensitiveProperty()
    {
        bool isRedacted = _service.IsSummaryRedacted(
            """{"payload":[{"connection_string":"Host=secret"}]}""");

        isRedacted.ShouldBeTrue();
    }

    [Fact]
    public void IsSummaryRedacted_Should_ReturnFalse_WhenSummaryContainsOnlySafeProperties()
    {
        bool isRedacted = _service.IsSummaryRedacted(
            """{"document_id":"abc","status":"Posted"}""");

        isRedacted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Create", "إنشاء", "Create")]
    [InlineData("Post", "ترحيل", "Post")]
    [InlineData("Submit", "إرسال للتدقيق", "Submit")]
    [InlineData("Transfer", "نقل", "Transfer")]
    public void GetActionDisplay_Should_ReturnCorrectBilingualLabels(string action, string expectedAr, string expectedEn)
    {
        _service.GetActionDisplayAr(action).ShouldBe(expectedAr);
        _service.GetActionDisplayEn(action).ShouldBe(expectedEn);
    }

    [Theory]
    [InlineData("WarehouseDocument", "مستند مستودعي", "Warehouse Document")]
    [InlineData("TrackedMaterialUnit", "وحدة مادة متتبعة (معمرة)", "Tracked Material Unit")]
    [InlineData("DurableCustodyAllocation", "تخصيص عهدة معمرة (كمية)", "Durable Custody Allocation")]
    public void GetEntityTypeDisplay_Should_ReturnCorrectBilingualLabels(string entityType, string expectedAr, string expectedEn)
    {
        _service.GetEntityTypeDisplayAr(entityType).ShouldBe(expectedAr);
        _service.GetEntityTypeDisplayEn(entityType).ShouldBe(expectedEn);
    }
}
