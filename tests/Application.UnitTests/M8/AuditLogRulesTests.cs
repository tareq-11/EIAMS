using System.Text;
using Domain.AuditLogs;
using SharedKernel;

namespace Application.UnitTests.M8;

public sealed class AuditLogRulesTests
{
    [Fact]
    public void Create_Should_ReturnSuccess_AndMapAllProperties_WhenEveryFieldIsValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        DateTime createdAtUtc = DateTime.UtcNow;

        // Act
        Result<AuditLog> result = AuditLog.Create(
            id,
            operationId,
            "request-1",
            userId,
            "User",
            entityId,
            "WarehouseDocument",
            aggregateId,
            AuditActions.Update,
            "Application.Users.RegisterUserCommand",
            """{"field":"value"}""",
            "192.168.0.1",
            createdAtUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        AuditLog auditLog = result.Value;
        auditLog.Id.ShouldBe(id);
        auditLog.OperationId.ShouldBe(operationId);
        auditLog.RequestId.ShouldBe("request-1");
        auditLog.UserId.ShouldBe(userId);
        auditLog.EntityType.ShouldBe("User");
        auditLog.EntityId.ShouldBe(entityId);
        auditLog.AggregateType.ShouldBe("WarehouseDocument");
        auditLog.AggregateId.ShouldBe(aggregateId);
        auditLog.Action.ShouldBe(AuditActions.Update);
        auditLog.CommandName.ShouldBe("Application.Users.RegisterUserCommand");
        auditLog.Summary.ShouldBe("""{"field":"value"}""");
        auditLog.IpAddress.ShouldBe("192.168.0.1");
        auditLog.CreatedAtUtc.ShouldBe(createdAtUtc);
        auditLog.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Should_RejectEmptyOperationId_WhenOperationIdentifierIsMissing()
    {
        // Arrange
        AuditLogSpec spec = new() { OperationId = Guid.Empty };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.OperationIdRequired);
    }

    [Fact]
    public void Create_Should_RejectEmptyRowId_WhenPrimaryIdentifierIsMissing()
    {
        // Arrange
        AuditLogSpec spec = new() { Id = Guid.Empty };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.OperationIdRequired);
    }

    [Fact]
    public void Create_Should_RejectEmptyEntityId_WhenEntityIdentifierIsMissing()
    {
        // Arrange
        AuditLogSpec spec = new() { EntityId = Guid.Empty };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.EntityIdRequired);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Create_Should_EnforceRequestIdLengthLimit_WhenRequestIdIsProvided(int? length, bool shouldSucceed)
    {
        // Arrange
        string? requestId = length is null ? null : new string('r', length.Value);
        AuditLogSpec spec = new() { RequestId = requestId };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        if (shouldSucceed)
        {
            result.IsSuccess.ShouldBeTrue();
        }
        else
        {
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(AuditLogErrors.RequestIdTooLong);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Spaceship")]
    [InlineData("user")]
    public void Create_Should_RejectUnknownEntityType_WhenEntityTypeIsNotInTheRegistry(string entityType)
    {
        // Arrange
        AuditLogSpec spec = new() { EntityType = entityType };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.EntityTypeInvalid);
    }

    [Fact]
    public void Create_Should_RejectOverlongEntityType_WhenEntityTypeExceedsOneHundredCharacters()
    {
        // Arrange
        AuditLogSpec spec = new() { EntityType = new string('a', 101) };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.EntityTypeInvalid);
    }

    [Fact]
    public void Create_Should_AcceptNullUserId_WhenActorIsAnonymous()
    {
        // Arrange
        AuditLogSpec spec = new() { UserId = null };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_RejectEmptyUserId_WhenActorIdentifierIsEmptyGuid()
    {
        // Arrange
        AuditLogSpec spec = new() { UserId = Guid.Empty };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.UserIdInvalid);
    }

    [Theory]
    [InlineData("Spaceship")]
    [InlineData("warehouse document")]
    public void Create_Should_RejectUnknownAggregateType_WhenAggregateTypeIsNotInTheRegistry(string aggregateType)
    {
        // Arrange
        AuditLogSpec spec = new() { AggregateType = aggregateType };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.AggregateTypeInvalid);
    }

    [Fact]
    public void Create_Should_RejectOverlongAggregateType_WhenAggregateTypeExceedsOneHundredCharacters()
    {
        // Arrange
        AuditLogSpec spec = new() { AggregateType = new string('a', 101) };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.AggregateTypeInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("create")]
    [InlineData("Approve")]
    [InlineData("Bogus")]
    public void Create_Should_RejectActionOutsideTheClosedVocabulary_WhenActionIsNotARegisteredMember(string action)
    {
        // Arrange
        AuditLogSpec spec = new() { Action = action };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.ActionInvalid);
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void Create_Should_EnforceCommandNameLengthLimit_WhenCommandNameIsProvided(int length, bool shouldSucceed)
    {
        // Arrange
        AuditLogSpec spec = new() { CommandName = new string('c', length) };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        if (shouldSucceed)
        {
            result.IsSuccess.ShouldBeTrue();
        }
        else
        {
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(AuditLogErrors.CommandNameTooLong);
        }
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("not-json")]
    public void Create_Should_RejectSummaryThatIsNotAJsonObject_WhenSummaryIsProvided(string summary)
    {
        // Arrange
        AuditLogSpec spec = new() { Summary = summary };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.SummaryNotJsonObject);
    }

    [Fact]
    public void Create_Should_AcceptSummaryAtExactlyTheByteBudget_WhenSummaryIs16384Utf8Bytes()
    {
        // Arrange
        string boundarySummary = "{\"k\":\"" + new string('a', 16376) + "\"}";
        Encoding.UTF8.GetByteCount(boundarySummary).ShouldBe(AuditLog.MaxSummaryBytes);
        AuditLogSpec spec = new() { Summary = boundarySummary };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_RejectOversizedSummary_WhenSummaryExceeds16384Utf8Bytes()
    {
        // Arrange
        string oversizedSummary = "{\"k\":\"" + new string('é', 8189) + "\"}";
        Encoding.UTF8.GetByteCount(oversizedSummary).ShouldBeGreaterThan(AuditLog.MaxSummaryBytes);
        AuditLogSpec spec = new() { Summary = oversizedSummary };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.SummaryTooLarge);
    }

    [Fact]
    public void Create_Should_RejectOverlongIpAddress_WhenIpAddressExceedsFortyFiveCharacters()
    {
        // Arrange
        AuditLogSpec spec = new() { IpAddress = new string('a', 46) };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.IpAddressTooLong);
    }

    [Fact]
    public void Create_Should_AcceptFortyFiveCharacterIpAddress_WhenIpAddressMatchesIpv6Maximum()
    {
        // Arrange
        AuditLogSpec spec = new() { IpAddress = new string('a', 45) };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_Should_RejectNonUtcTimestamp_WhenKindIsUnspecified()
    {
        // Arrange
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        AuditLogSpec spec = new() { CreatedAtUtc = unspecified };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.TimestampNotUtc);
    }

    [Fact]
    public void Create_Should_RejectNonUtcTimestamp_WhenKindIsLocal()
    {
        // Arrange
        AuditLogSpec spec = new() { CreatedAtUtc = DateTime.Now };

        // Act
        Result<AuditLog> result = CreateFrom(spec);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.TimestampNotUtc);
    }

    [Fact]
    public void AuditActions_Should_ExposeExactlyTwentyFourActions_WhenVocabularyIsClosed()
    {
        // Arrange

        // Act
        int count = AuditActions.All.Count;

        // Assert
        count.ShouldBe(24);
    }

    [Theory]
    [InlineData(AuditActions.Create)]
    [InlineData(AuditActions.Update)]
    [InlineData(AuditActions.Delete)]
    [InlineData(AuditActions.Submit)]
    [InlineData(AuditActions.Post)]
    [InlineData(AuditActions.Reject)]
    [InlineData(AuditActions.Cancel)]
    [InlineData(AuditActions.Reverse)]
    [InlineData(AuditActions.ReturnToDraft)]
    [InlineData(AuditActions.Move)]
    [InlineData(AuditActions.Activate)]
    [InlineData(AuditActions.Deactivate)]
    [InlineData(AuditActions.Add)]
    [InlineData(AuditActions.Remove)]
    [InlineData(AuditActions.RecordActual)]
    [InlineData(AuditActions.Start)]
    [InlineData(AuditActions.Complete)]
    [InlineData(AuditActions.Close)]
    [InlineData(AuditActions.Assign)]
    [InlineData(AuditActions.Grant)]
    [InlineData(AuditActions.Revoke)]
    [InlineData(AuditActions.Authenticate)]
    [InlineData(AuditActions.TokenRefresh)]
    [InlineData(AuditActions.Logout)]
    public void AuditActions_Should_ContainRegisteredAction_WhenActionBelongsToTheVocabulary(string action)
    {
        // Arrange

        // Act
        bool isKnown = AuditActions.All.Contains(action);

        // Assert
        isKnown.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("create")]
    [InlineData("RETURN_TO_DRAFT")]
    public void AuditActions_Should_RejectNonMembers_WhenActionIsReservedOrWrongCase(string action)
    {
        // Arrange

        // Act
        bool isKnown = AuditActions.All.Contains(action);

        // Assert
        isKnown.ShouldBeFalse();
    }

    [Fact]
    public void KnownAuditEntityTypes_Should_ExposeStableNames_WhenRegistryIsSealed()
    {
        // Arrange

        // Act
        int count = KnownAuditEntityTypes.All.Count;

        // Assert
        count.ShouldBe(43);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Role")]
    [InlineData("RoleAllowedScopeType")]
    [InlineData("Organization")]
    [InlineData("Material")]
    [InlineData("WarehouseDocument")]
    [InlineData("StockMovement")]
    [InlineData("Asset")]
    [InlineData("Custody")]
    [InlineData("InventoryCount")]
    [InlineData("InventoryAdjustment")]
    [InlineData("AdjustmentLine")]
    public void KnownAuditEntityTypes_Should_MatchRegisteredEntityType_WhenQueriedByName(string entityType)
    {
        // Arrange

        // Act
        bool isKnown = KnownAuditEntityTypes.IsKnown(entityType);

        // Assert
        isKnown.ShouldBeTrue();
    }

    [Theory]
    [InlineData("user")]
    [InlineData("Unknown")]
    [InlineData("")]
    public void KnownAuditEntityTypes_Should_RejectUnregisteredNames_WhenQueriedCaseSensitively(string entityType)
    {
        // Arrange

        // Act
        bool isKnown = KnownAuditEntityTypes.IsKnown(entityType);

        // Assert
        isKnown.ShouldBeFalse();
    }

    [Fact]
    public void KnownAuditEntityTypes_Should_UsePascalCaseIdentifiersOnly_WhenEnumeratingRegistry()
    {
        // Arrange

        // Act
        bool allCanonical = KnownAuditEntityTypes.All.All(entityType =>
            char.IsUpper(entityType[0]) &&
            entityType.Skip(1).All(character => char.IsLetterOrDigit(character)));

        // Assert
        allCanonical.ShouldBeTrue();
    }

    [Fact]
    public void EntryCreate_Should_ReturnSuccess_AndMapProperties_WhenBothValuesArePresentAndDifferent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var auditLogId = Guid.NewGuid();

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            id,
            auditLogId,
            "status_code",
            "Draft",
            "Submitted");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        AuditLogEntry entry = result.Value;
        entry.Id.ShouldBe(id);
        entry.AuditLogId.ShouldBe(auditLogId);
        entry.FieldName.ShouldBe("status_code");
        entry.OldValue.ShouldBe("Draft");
        entry.NewValue.ShouldBe("Submitted");
        entry.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EntryCreate_Should_AcceptSingleSidedChange_WhenOnlyOldOrNewValueIsProvided(bool hasOld, bool hasNew)
    {
        // Arrange
        string? oldValue = hasOld ? "Draft" : null;
        string? newValue = hasNew ? "Submitted" : null;

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "status_code",
            oldValue,
            newValue);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("StatusCode")]
    [InlineData("1field")]
    [InlineData("_field")]
    [InlineData("with-dash")]
    [InlineData("with space")]
    public void EntryCreate_Should_RejectFieldNameViolatingSnakeCase_WhenPatternDoesNotMatch(string fieldName)
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fieldName,
            "old",
            "new");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.FieldNameInvalid);
    }

    [Fact]
    public void EntryCreate_Should_RejectOverlongFieldName_WhenFieldNameExceedsOneHundredCharacters()
    {
        // Arrange
        string fieldName = new('a', 101);

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fieldName,
            "old",
            "new");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.FieldNameInvalid);
    }

    [Fact]
    public void EntryCreate_Should_RejectNullPair_WhenNeitherOldNorNewValueIsProvided()
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "status_code",
            null,
            null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.FieldChangeRequired);
    }

    [Fact]
    public void EntryCreate_Should_RejectNoChangePair_WhenOldAndNewValuesAreEqual()
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "status_code",
            "same",
            "same");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.NoChangePair);
    }

    [Fact]
    public void EntryCreate_Should_TreatEmptyStringsAsDistinct_WhenOldAndNewDifferOnlyInEmptiness()
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "note",
            null,
            "");

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EntryCreate_Should_RejectEmptyEntryId_WhenEntryIdentifierIsMissing()
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "status_code",
            "old",
            "new");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.IdentityRequired);
    }

    [Fact]
    public void EntryCreate_Should_RejectEmptyParentId_WhenAuditLogIdentifierIsMissing()
    {
        // Arrange

        // Act
        Result<AuditLogEntry> result = AuditLogEntry.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "status_code",
            "old",
            "new");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogEntryErrors.IdentityRequired);
    }

    private static Result<AuditLog> CreateFrom(AuditLogSpec spec) => AuditLog.Create(
        spec.Id,
        spec.OperationId,
        spec.RequestId,
        spec.UserId,
        spec.EntityType,
        spec.EntityId,
        spec.AggregateType,
        spec.AggregateId,
        spec.Action,
        spec.CommandName,
        spec.Summary,
        spec.IpAddress,
        spec.CreatedAtUtc);

    private sealed record AuditLogSpec
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid OperationId { get; init; } = Guid.NewGuid();
        public string? RequestId { get; init; } = "request-1";
        public Guid? UserId { get; init; }
        public string EntityType { get; init; } = "User";
        public Guid EntityId { get; init; } = Guid.NewGuid();
        public string? AggregateType { get; init; }
        public Guid? AggregateId { get; init; } = Guid.NewGuid();
        public string Action { get; init; } = AuditActions.Create;
        public string? CommandName { get; init; }
        public string? Summary { get; init; } = """{"source":"unit-test"}""";
        public string? IpAddress { get; init; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
