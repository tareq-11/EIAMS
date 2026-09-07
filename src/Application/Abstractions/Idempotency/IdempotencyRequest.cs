using System.Security.Cryptography;
using System.Text;

namespace Application.Abstractions.Idempotency;

public sealed record IdempotencyRequest(Guid Key, string Operation, string RequestHash)
{
    public static IdempotencyRequest Create(Guid key, string operation, string canonicalRequest)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest));
        return new IdempotencyRequest(key, operation, Convert.ToHexString(hash));
    }
}
