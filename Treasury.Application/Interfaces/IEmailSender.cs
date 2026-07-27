namespace Treasury.Application.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }

    void EnsureConfigured();

    Task SendUserInvitation(
        string recipientEmail,
        string recipientName,
        string organizationName,
        string acceptanceUrl,
        DateTime expiresAtUtc);

    Task SendPasswordReset(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc);
}
