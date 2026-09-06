namespace Application.Users;

internal static class UserSessionLock
{
    internal static string ForUser(Guid userId) => $"security:user-session:{userId:D}";

    internal static string ForRefreshToken(string tokenHash) => $"security:refresh-token:{tokenHash}";
}
