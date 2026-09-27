namespace Infrastructure.Authentication;

internal sealed class BootstrapAdministratorOptions
{
    internal const string SectionName = "BootstrapAdministrator";

    public bool Enabled { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    internal static bool IsValid(BootstrapAdministratorOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.FirstName) ||
            string.IsNullOrWhiteSpace(options.LastName))
        {
            return false;
        }

        string normalizedUsername = Domain.Users.User.NormalizeUsername(options.Username);
        bool validEmail = !string.IsNullOrWhiteSpace(options.Email) &&
                          options.Email.Length <= 256 &&
                          options.Email.All(character => character <= '\u007F') &&
                          new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(options.Email);
        bool validPassword = !string.IsNullOrWhiteSpace(options.Password) &&
                             options.Password.Length is >= 8 and <= 128 &&
                             options.Password.Any(character => character is >= 'A' and <= 'Z') &&
                             options.Password.Any(character => character is >= 'a' and <= 'z') &&
                             options.Password.Any(character => character is >= '0' and <= '9') &&
                             options.Password.Any(character => !char.IsLetterOrDigit(character));

        return validEmail &&
               Domain.Users.User.IsValidUsername(normalizedUsername) &&
               options.FirstName.Length <= 200 &&
               options.LastName.Length <= 200 &&
               validPassword;
    }
}
