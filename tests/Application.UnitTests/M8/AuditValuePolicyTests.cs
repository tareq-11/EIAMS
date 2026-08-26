using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Audit;
using Domain.Common;
using Domain.WarehouseDocuments;
using Infrastructure.AuditLogs;

namespace Application.UnitTests.M8;

public sealed class AuditValuePolicyTests
{
    private readonly AuditValuePolicy policy = new();

    [Theory]
    [InlineData("id")]
    [InlineData("created_at_utc")]
    [InlineData("created_by")]
    [InlineData("updated_at_utc")]
    [InlineData("updated_by")]
    [InlineData("row_version")]
    public void IsExcludedField_Should_ReturnTrue_WhenFieldIsCaptureNoise(string field)
    {
        // Act
        bool excluded = policy.IsExcludedField("Organization", field);

        // Assert
        excluded.ShouldBeTrue();
    }

    [Fact]
    public void IsExcludedField_Should_ReturnFalse_WhenFieldIsBusinessData()
    {
        // Act
        bool excluded = policy.IsExcludedField("Organization", "name");

        // Assert
        excluded.ShouldBeFalse();
    }

    [Theory]
    [InlineData("User", "password_hash")]
    [InlineData("RefreshToken", "token")]
    [InlineData("DocumentAttachment", "storage_key")]
    public void IsForbidden_Should_ReturnTrue_WhenFieldHoldsSecretMaterial(string entityType, string field)
    {
        // Act
        bool forbidden = policy.IsForbidden(entityType, field);

        // Assert
        forbidden.ShouldBeTrue();
    }

    [Fact]
    public void IsForbidden_Should_ReturnFalse_WhenFieldIsSafe()
    {
        // Arrange
        (string EntityType, string Field)[] safeFields =
        [
            ("User", "email"),
            ("DocumentAttachment", "original_filename"),
            ("RefreshToken", "expires_on_utc")
        ];

        // Act + Assert
        foreach ((string entityType, string field) in safeFields)
        {
            policy.IsForbidden(entityType, field).ShouldBeFalse($"{entityType}.{field}");
        }
    }

    [Fact]
    public void Serialize_Should_FormatGuidInDFormat_WhenValueIsGuid()
    {
        // Arrange
        var guid = new Guid("01234567-89AB-CDEF-0123-456789ABCDEF");

        // Act
        string? serialized = policy.Serialize("Organization", "id", guid);

        // Assert
        serialized.ShouldBe("01234567-89ab-cdef-0123-456789abcdef");
    }

    [Fact]
    public void Serialize_Should_WriteEnumName_WhenValueIsEnum()
    {
        // Act
        string? serialized = policy.Serialize("WarehouseDocument", "document_status", DocumentStatus.Posted);

        // Assert
        serialized.ShouldBe("Posted");
    }

    [Fact]
    public void Serialize_Should_UseInvariantCulture_WhenValueIsDecimalUnderNonInvariantCulture()
    {
        // Arrange
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE", useUserOverride: false);

            // Act
            string? serialized = policy.Serialize("InventoryBalance", "quantity", 1234.567m);

            // Assert
            serialized.ShouldBe("1234.567");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(-7L, "-7")]
    public void Serialize_Should_UseInvariantCulture_WhenValueIsIntegralNumber(object value, string expected)
    {
        // Act
        string? serialized = policy.Serialize("Material", "reorder_point", value);

        // Assert
        serialized.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, "True")]
    [InlineData(false, "False")]
    public void Serialize_Should_UseInvariantBooleanText_WhenValueIsBoolean(bool value, string expected)
    {
        // Act
        string? serialized = policy.Serialize("Warehouse", "can_hold_stock", value);

        // Assert
        serialized.ShouldBe(expected);
    }

    [Fact]
    public void Serialize_Should_ProduceIso8601OFormatUtcString_WhenValueIsUtcDateTime()
    {
        // Arrange
        DateTime timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1235678);

        // Act
        string? serialized = policy.Serialize("Organization", "posted_at_utc", timestamp);

        // Assert
        serialized.ShouldBe("2026-01-02T03:04:05.1235678Z");
    }

    [Fact]
    public void Serialize_Should_ConvertToUtcBeforeFormatting_WhenValueIsLocalDateTime()
    {
        // Arrange
        var utc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime local = utc.ToLocalTime();

        // Act
        string? serialized = policy.Serialize("Organization", "updated_at_utc", local);

        // Assert
        serialized.ShouldBe(utc.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Serialize_Should_TreatUnspecifiedKindAsUtc_WhenValueIsUnspecifiedDateTime()
    {
        // Arrange
        var unspecified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);

        // Act
        string? serialized = policy.Serialize("Organization", "moved_at_utc", unspecified);

        // Assert
        serialized.ShouldBe("2026-01-02T03:04:05.0000000Z");
    }

    [Fact]
    public void Serialize_Should_ReturnNull_WhenValueIsNull()
    {
        // Act
        string? serialized = policy.Serialize("Organization", "name", null);

        // Assert
        serialized.ShouldBeNull();
    }

    [Fact]
    public void Serialize_Should_ReturnNull_WhenValueIsByteArray()
    {
        // Arrange
        byte[] binary = [1, 2, 3];

        // Act
        string? serialized = policy.Serialize("DocumentAttachment", "file_bytes", binary);

        // Assert
        serialized.ShouldBeNull();
    }

    [Fact]
    public void Serialize_Should_StoreFilenameExactly_WhenUtf8SizeIsExactly1024Bytes()
    {
        // Arrange
        string filename = new('a', 1024);

        // Act
        string? serialized = policy.Serialize("DocumentAttachment", "original_filename", filename);

        // Assert
        serialized.ShouldBe(filename);
    }

    [Fact]
    public void Serialize_Should_StoreMultibyteFilenameExactly_WhenUtf8SizeIsExactly1024Bytes()
    {
        // Arrange
        string filename = new('é', 512);

        Encoding.UTF8.GetByteCount(filename).ShouldBe(1024);

        // Act
        string? serialized = policy.Serialize("DocumentAttachment", "original_filename", filename);

        // Assert
        serialized.ShouldBe(filename);
    }

    [Fact]
    public void Serialize_Should_StoreSha256Marker_WhenFilenameIs1025Bytes()
    {
        // Arrange
        string filename = new('a', 1025);
        string expectedMarker = ExpectedMarker(filename);

        // Act
        string? serialized = policy.Serialize("DocumentAttachment", "original_filename", filename);

        // Assert
        serialized.ShouldBe(expectedMarker);
        serialized.ShouldNotBe(filename);
    }

    [Fact]
    public void Serialize_Should_StoreExactValue_WhenStringIsExactly16KiB()
    {
        // Arrange
        string value = new('b', 16384);

        // Act
        string? serialized = policy.Serialize("Material", "attributes", value);

        // Assert
        serialized.ShouldBe(value);
    }

    [Fact]
    public void Serialize_Should_StoreMarkerWithLength_WhenStringExceeds16KiB()
    {
        // Arrange
        string value = new('b', 16385);

        // Act
        string? serialized = policy.Serialize("Material", "attributes", value);

        // Assert
        serialized.ShouldBe($"sha256:{ExpectedHex(value)} (len:16385)");
    }

    [Fact]
    public void Serialize_Should_NeverTruncateLongStrings_WhenValueIsBelowMarkerThreshold()
    {
        // Arrange
        string value = new('x', 16000);

        // Act
        string? serialized = policy.Serialize("Material", "description", value);

        // Assert
        serialized.ShouldNotBeNull();
        serialized.Length.ShouldBe(value.Length);
        serialized.ShouldBe(value);
    }

    [Fact]
    public void Serialize_Should_BeDeterministic_WhenCalledRepeatedly()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        string? first = policy.Serialize("Organization", "id", value);
        string? second = policy.Serialize("Organization", "id", value);

        // Assert
        first.ShouldNotBeNull();
        first.ShouldBe(second);
    }

    private static string ExpectedMarker(string value) => $"sha256:{ExpectedHex(value)}";

    private static string ExpectedHex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
