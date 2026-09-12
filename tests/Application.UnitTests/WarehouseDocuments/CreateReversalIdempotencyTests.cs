using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Idempotency;
using Application.Abstractions.Numbering;
using Application.UnitTests.Abstractions;
using Application.WarehouseDocuments.CreateReversal;
using Domain.Common;
using Domain.WarehouseDocuments;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.WarehouseDocuments;

public sealed class CreateReversalIdempotencyTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReplayStoredReversalBeforeReevaluatingDocumentState()
    {
        await using TestDbContext context = CreateDbContext();
        var actorUserId = Guid.NewGuid();
        var source = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DocumentType.Receiving,
            "SOURCE-001");
        context.WarehouseDocuments.Add(source);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(actorUserId);
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                actorUserId,
                Arg.Any<string>(),
                ScopeType.Warehouse,
                source.WarehouseId,
                Arg.Any<CancellationToken>())
            .Returns(true);
        IApplicationTransaction transaction = Substitute.For<IApplicationTransaction>();
        transaction.ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<Guid>>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        IIdempotencyService idempotency = Substitute.For<IIdempotencyService>();
        var reversalId = Guid.NewGuid();
        idempotency.TryBeginAsync<Guid>(
                Arg.Any<IdempotencyRequest>(),
                actorUserId,
                Arg.Any<CancellationToken>())
            .Returns(new IdempotencyReplay<Guid>(true, reversalId));
        IReferenceNumberGenerator numberGenerator = Substitute.For<IReferenceNumberGenerator>();
        var handler = new CreateReversalDocumentCommandHandler(
            context,
            userContext,
            authorization,
            numberGenerator,
            Substitute.For<IDatabaseExceptionClassifier>(),
            transaction,
            idempotency);

        Result<Guid> result = await handler.Handle(
            new CreateReversalDocumentCommand(source.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(reversalId);
        await numberGenerator.DidNotReceive().AllocateAsync(
            Arg.Any<Guid>(),
            Arg.Any<DocumentType>(),
            Arg.Any<CancellationToken>());
    }
}
