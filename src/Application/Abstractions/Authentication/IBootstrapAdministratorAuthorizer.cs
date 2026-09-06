namespace Application.Abstractions.Authentication;

public interface IBootstrapAdministratorAuthorizer
{
    bool IsAuthorized(string? bootstrapToken);
}
