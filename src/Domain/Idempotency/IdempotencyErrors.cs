using SharedKernel;

namespace Domain.Idempotency;

public static class IdempotencyErrors
{
    public static readonly Error KeyReused = Error.Conflict(
        "Idempotency.KeyReused",
        "The Idempotency-Key was already used for a different request.");
}
