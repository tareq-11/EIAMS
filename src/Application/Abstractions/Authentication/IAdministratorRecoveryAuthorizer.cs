namespace Application.Abstractions.Authentication;

public interface IAdministratorRecoveryAuthorizer
{
    bool IsAuthorized(string? recoveryToken);
}
