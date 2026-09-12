using System.Text.Json;
using Application.Abstractions.Data;
using Application.Abstractions.Idempotency;
using Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Idempotency;

internal sealed class IdempotencyService(
    IApplicationDbContext context,
    IApplicationLock applicationLock,
    IDateTimeProvider dateTimeProvider) : IIdempotencyService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<Result<IdempotencyReplay<TResponse>>> TryBeginAsync<TResponse>(
        IdempotencyRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await applicationLock.AcquireAsync($"idempotency:{request.Key:D}", cancellationToken);

        IdempotencyRecord? existing = await context.IdempotencyRecords
            .SingleOrDefaultAsync(record => record.Key == request.Key, cancellationToken);

        if (existing is null)
        {
            return new IdempotencyReplay<TResponse>(false, default);
        }

        if (existing.ExpiresAtUtc <= dateTimeProvider.UtcNow)
        {
            context.IdempotencyRecords.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            return new IdempotencyReplay<TResponse>(false, default);
        }

        if (existing.ActorUserId != actorUserId ||
            !string.Equals(existing.Operation, request.Operation, StringComparison.Ordinal) ||
            !string.Equals(existing.RequestHash, request.RequestHash, StringComparison.Ordinal))
        {
            return Result.Failure<IdempotencyReplay<TResponse>>(IdempotencyErrors.KeyReused);
        }

        TResponse? response = JsonSerializer.Deserialize<TResponse>(existing.ResponsePayload);
        return response is null
            ? throw new InvalidOperationException($"Stored idempotency response for key {request.Key} is invalid.")
            : new IdempotencyReplay<TResponse>(true, response);
    }

    public void Complete<TResponse>(
        IdempotencyRequest request,
        Guid actorUserId,
        TResponse response)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            request.Key,
            request.Operation,
            actorUserId,
            request.RequestHash,
            JsonSerializer.Serialize(response),
            nowUtc,
            nowUtc.Add(Retention)));
    }
}
