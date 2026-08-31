using Domain.AssetMovementHistories;
using Domain.AuditLogs;
using Domain.Common;
using Domain.CustodyHistories;
using Domain.DocumentLines;
using Domain.InventoryCounts;
using Domain.Roles;
using Infrastructure.AuditLogs;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel;

namespace Application.UnitTests.M8;

public sealed class AuditEntityRegistryTests
{
    private readonly AuditEntityRegistry registry = new();

    [Fact]
    public void Registry_Should_MapDocumentLineToWarehouseDocumentAggregate_WhenQueried()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        Result<DocumentLine> creation = DocumentLine.Create(
            Guid.NewGuid(),
            documentId,
            Guid.NewGuid(),
            DocumentLineType.Normal,
            5m,
            null,
            5m,
            null,
            null,
            null);

        creation.IsSuccess.ShouldBeTrue();
        DocumentLine line = creation.Value;

        // Act
        bool resolved = registry.TryGet(line.GetType(), out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("DocumentLine");
        mapping.AggregateType.ShouldBe("WarehouseDocument");
        mapping.EntityIdSelector!(line).ShouldBe(line.Id);
        mapping.AggregateIdSelector!(line).ShouldBe(documentId);
    }

    [Fact]
    public void Registry_Should_AdaptRolePermissionToRoleAggregate_WhenQueried()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var rolePermission = RolePermission.Create(roleId, permissionId);
        // Act
        bool resolved = registry.TryGet(rolePermission.GetType(), out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("RolePermission");
        mapping.AggregateType.ShouldBe("Role");
        mapping.EntityIdSelector!(rolePermission).ShouldBe(roleId);
        mapping.AggregateIdSelector!(rolePermission).ShouldBe(roleId);
    }

    [Fact]
    public void Registry_Should_MapAllowedScopeTypeToKnownRoleAggregate_WhenQueried()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var allowedScopeType = RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse);

        // Act
        bool resolved = registry.TryGet(
            allowedScopeType.GetType(),
            out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("RoleAllowedScopeType");
        KnownAuditEntityTypes.IsKnown(mapping.EntityType).ShouldBeTrue();
        mapping.AggregateType.ShouldBe("Role");
        mapping.EntityIdSelector(allowedScopeType).ShouldBe(roleId);
        mapping.AggregateIdSelector!(allowedScopeType).ShouldBe(roleId);
    }

    [Fact]
    public void Registry_Should_MapCustodyHistoryToCustodyAggregate_WhenQueried()
    {
        // Arrange
        var custodyId = Guid.NewGuid();
        Result<CustodyHistory> creation = CustodyHistory.Create(
            Guid.NewGuid(),
            custodyId,
            CustodyStatus.Active,
            CustodyStatus.Closed,
            Guid.NewGuid(),
            DateTime.UtcNow,
            null);

        creation.IsSuccess.ShouldBeTrue();
        CustodyHistory history = creation.Value;

        // Act
        bool resolved = registry.TryGet(history.GetType(), out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("CustodyHistory");
        mapping.AggregateType.ShouldBe("Custody");
        mapping.AggregateIdSelector!(history).ShouldBe(custodyId);
    }

    [Fact]
    public void Registry_Should_MapCountLineToInventoryCountAggregate_WhenQueried()
    {
        // Arrange
        var countId = Guid.NewGuid();
        Result<InventoryCountLine> creation = InventoryCountLine.Create(
            Guid.NewGuid(),
            countId,
            Guid.NewGuid(),
            null,
            10m);

        creation.IsSuccess.ShouldBeTrue();
        InventoryCountLine line = creation.Value;

        // Act
        bool resolved = registry.TryGet(line.GetType(), out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("InventoryCountLine");
        mapping.AggregateType.ShouldBe("InventoryCount");
        mapping.AggregateIdSelector!(line).ShouldBe(countId);
    }

    [Fact]
    public void Registry_Should_MapAssetMovementHistoryToAssetAggregate_WhenQueried()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        Result<AssetMovementHistory> creation = AssetMovementHistory.Create(
            Guid.NewGuid(),
            assetId,
            Guid.NewGuid(),
            AssetMovementType.Received,
            DateTime.UtcNow);

        creation.IsSuccess.ShouldBeTrue();
        AssetMovementHistory history = creation.Value;

        // Act
        bool resolved = registry.TryGet(history.GetType(), out AuditEntityRegistry.Mapping? mapping);

        // Assert
        resolved.ShouldBeTrue();
        mapping!.EntityType.ShouldBe("AssetMovementHistory");
        mapping.AggregateType.ShouldBe("Asset");
        mapping.AggregateIdSelector!(history).ShouldBe(assetId);
    }

    [Fact]
    public void Registry_Should_ExcludeTechnicalTypesWithReason_WhenQueried()
    {
        // Act + Assert
        foreach (KeyValuePair<Type, string> exclusion in AuditEntityRegistry.TechnicalExclusions)
        {
            registry.TryGet(exclusion.Key, out _).ShouldBeFalse(
                $"{exclusion.Key.Name} is both mapped and technically excluded.");

            exclusion.Value.ShouldNotBeNullOrWhiteSpace(
                $"{exclusion.Key.Name} must carry a written exclusion reason.");
        }
    }

    [Fact]
    public void TechnicalExclusions_Should_ContainTheKnownTechnicalEntities()
    {
        // Arrange
        string[] expectedNames =
        [
            "AuditLog",
            "AuditLogEntry",
            "RefreshToken",
            "PendingFileDeletion",
            "DocumentSequence",
            "AssetCurrentStatusView"
        ];

        // Act + Assert
        foreach (string name in expectedNames)
        {
            AuditEntityRegistry.TechnicalExclusions.Keys.ShouldContain(
                type => type.Name == name,
                $"{name} must be classified as a technical exclusion.");
        }
    }

    [Fact]
    public void Registry_Should_ClassifyEveryMappedEntityType_WhenApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using ApplicationDbContext context = BuildContextWithoutConnecting();

        List<string> offenders = [];

        // Act
        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.GetTableName() is null)
            {
                continue;
            }

            Type clrType = entityType.ClrType;

            if (registry.TryGet(clrType, out _))
            {
                continue;
            }

            if (AuditEntityRegistry.TechnicalExclusions.ContainsKey(clrType))
            {
                continue;
            }

            offenders.Add(clrType.Name);
        }

        // Assert
        offenders.ShouldBeEmpty(
            "Every mutable entity in the model must be audit-mapped or explicitly excluded with a reason.");
    }

    private static ApplicationDbContext BuildContextWithoutConnecting()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=dummy");

        return new ApplicationDbContext(optionsBuilder.Options, new NoOpDomainEventsDispatcher());
    }

    private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
