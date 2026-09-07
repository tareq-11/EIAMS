using SharedKernel;

namespace Application.Abstractions.Idempotency;

public interface IIdempotencyService
{
    Task<Result<IdempotencyReplay<TResponse>>> TryBeginAsync<TResponse>(
        IdempotencyRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken);

    void Complete<TResponse>(
        IdempotencyRequest request,
        Guid actorUserId,
        TResponse response);
}

public sealed record IdempotencyReplay<TResponse>(bool HasResponse, TResponse? Response);
