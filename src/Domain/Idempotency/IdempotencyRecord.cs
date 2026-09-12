namespace Domain.Idempotency;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public Guid Key { get; private set; }

    public string Operation { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string RequestHash { get; private set; }

    public string ResponsePayload { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public static IdempotencyRecord Create(
        Guid key,
        string operation,
        Guid actorUserId,
        string requestHash,
        string responsePayload,
        DateTime createdAtUtc,
        DateTime expiresAtUtc) => new()
        {
            Key = key,
            Operation = operation,
            ActorUserId = actorUserId,
            RequestHash = requestHash,
            ResponsePayload = responsePayload,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
}
