using System.Net.Mail;

namespace Treasury.Infrastructure.Persistence;

public class PlatformAdminBootstrapOptions
{
    public const string SectionName =
        "PlatformAdminBootstrap";

    public bool Enabled { get; set; }

    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string Password { get; set; } =
        string.Empty;

    public bool IsValid()
    {
        if (!Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(FirstName) ||
            FirstName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(LastName) ||
            LastName.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(Email) ||
            Email.Trim().Length > 320 ||
            !MailAddress.TryCreate(
                Email.Trim(),
                out _))
        {
            return false;
        }

        return Password.Length is >= 12 and <= 128 &&
               Password.Any(char.IsUpper) &&
               Password.Any(char.IsLower) &&
               Password.Any(char.IsDigit) &&
               Password.Any(character =>
                   !char.IsLetterOrDigit(character));
    }
}