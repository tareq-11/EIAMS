using Application.Abstractions.Data;
using Application.Abstractions.Idempotency;
using Application.UnitTests.Abstractions;
using Domain.Idempotency;
using Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.Idempotency;

public sealed class IdempotencyServiceTests : BaseHandlerTest
{
    [Fact]
    public async Task TryBegin_Should_ReplayMatchingResponse_AndRejectDifferentRequestOrActor()
    {
        await using TestDbContext context = CreateDbContext();
        DateTime nowUtc = DateTime.UtcNow;
        IApplicationLock applicationLock = Substitute.For<IApplicationLock>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);
        var service = new IdempotencyService(context, applicationLock, dateTimeProvider);
        var key = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var request = IdempotencyRequest.Create(key, "document.post", "request-a");
        var response = new StoredResponse(Guid.NewGuid(), "completed");

        Result<IdempotencyReplay<StoredResponse>> first = await service.TryBeginAsync<StoredResponse>(
            request,
            actorUserId,
            CancellationToken.None);
        first.IsSuccess.ShouldBeTrue();
        first.Value.HasResponse.ShouldBeFalse();

        service.Complete(request, actorUserId, response);
        await context.SaveChangesAsync();

        Result<IdempotencyReplay<StoredResponse>> replay = await service.TryBeginAsync<StoredResponse>(
            request,
            actorUserId,
            CancellationToken.None);
        replay.IsSuccess.ShouldBeTrue();
        replay.Value.HasResponse.ShouldBeTrue();
        replay.Value.Response.ShouldBe(response);

        Result<IdempotencyReplay<StoredResponse>> changedRequest =
            await service.TryBeginAsync<StoredResponse>(
                IdempotencyRequest.Create(key, "document.post", "request-b"),
                actorUserId,
                CancellationToken.None);
        changedRequest.IsFailure.ShouldBeTrue();
        changedRequest.Error.ShouldBe(IdempotencyErrors.KeyReused);

        Result<IdempotencyReplay<StoredResponse>> changedActor =
            await service.TryBeginAsync<StoredResponse>(
                request,
                Guid.NewGuid(),
                CancellationToken.None);
        changedActor.IsFailure.ShouldBeTrue();
        changedActor.Error.ShouldBe(IdempotencyErrors.KeyReused);

        await applicationLock.Received(4).AcquireAsync(
            $"idempotency:{key:D}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryBegin_Should_DeleteExpiredRecordAndAllowKeyReuse()
    {
        await using TestDbContext context = CreateDbContext();
        DateTime nowUtc = DateTime.UtcNow;
        var key = Guid.NewGuid();
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            key,
            "document.post",
            Guid.NewGuid(),
            new string('A', 64),
            "{}",
            nowUtc.AddDays(-2),
            nowUtc.AddDays(-1)));
        await context.SaveChangesAsync();

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);
        var service = new IdempotencyService(
            context,
            Substitute.For<IApplicationLock>(),
            dateTimeProvider);

        Result<IdempotencyReplay<StoredResponse>> result = await service.TryBeginAsync<StoredResponse>(
            IdempotencyRequest.Create(key, "another.operation", "new-request"),
            Guid.NewGuid(),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasResponse.ShouldBeFalse();
        (await context.IdempotencyRecords.AnyAsync(record => record.Key == key)).ShouldBeFalse();
    }

    private sealed record StoredResponse(Guid Id, string Status);
}
