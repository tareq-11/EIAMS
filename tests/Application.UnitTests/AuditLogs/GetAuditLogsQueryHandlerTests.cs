using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Pagination;
using Application.AuditLogs;
using Application.AuditLogs.GetAuditLogs;
using Application.UnitTests.Abstractions;
using Domain.AuditLogs;
using Domain.Common;
using Infrastructure.AuditLogs;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Application.UnitTests.AuditLogs;

public sealed class GetAuditLogsQueryHandlerTests
{
    private readonly IUserContext _userContext = Substitute.For<IUserContext>();
    private readonly IScopeAuthorizationService _scopeAuthorizationService = Substitute.For<IScopeAuthorizationService>();
    private readonly AuditRedactionService _redactionService = new();

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenUserLacksPermission()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);
        _scopeAuthorizationService.HasPermissionAsync(userId, PermissionCodes.AuditLogs.View, Arg.Any<CancellationToken>())
            .Returns(false);

        using TestDbContext context = CreateDbContext();
        var handler = new GetAuditLogsQueryHandler(context, _userContext, _scopeAuthorizationService, _redactionService);
        var query = new GetAuditLogsQuery();

        // Act
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationFailure_WhenGlobalFiltersAreInvalid()
    {
        // Arrange
        using TestDbContext context = CreateDbContext();
        var handler = new GetAuditLogsQueryHandler(context, _userContext, _scopeAuthorizationService, _redactionService);
        var query = new GetAuditLogsQuery(
            EntityType: "Unknown",
            FromUtc: DateTimeOffset.UtcNow,
            ToUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            Page: 0,
            PageSize: 9999);

        // Act
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuditLogErrors.FilterInvalid);
        await _scopeAuthorizationService.DidNotReceive()
            .HasPermissionAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnPagedAuditLogs_WhenUserHasEnterpriseScope()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);
        _scopeAuthorizationService.HasPermissionAsync(userId, PermissionCodes.AuditLogs.View, Arg.Any<CancellationToken>())
            .Returns(true);
        _scopeAuthorizationService.GetUserAssignmentAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAuthorizationAssignment(Guid.NewGuid(), userId, Guid.NewGuid(), ScopeType.Enterprise, null));

        using TestDbContext context = CreateDbContext();
        Result<AuditLog> log = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "REQ-1",
            userId,
            "WarehouseDocument",
            Guid.NewGuid(),
            null,
            null,
            "Post",
            "PostWarehouseDocumentCommand",
            null,
            "127.0.0.1",
            DateTime.UtcNow);
        log.IsSuccess.ShouldBeTrue();
        context.AuditLogs.Add(log.Value);
        await context.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(context, _userContext, _scopeAuthorizationService, _redactionService);
        var query = new GetAuditLogsQuery();

        // Act
        Result<PagedResult<AuditLogListItemResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalItems.ShouldBe(1);
        result.Value.Items[0].ActionDisplayAr.ShouldBe("ترحيل");
        result.Value.Items[0].EntityTypeDisplayAr.ShouldBe("مستند مستودعي");
    }

    private static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options);
    }
}
