using System.Text.Encodings.Web;

namespace Treasury.Infrastructure.Services;

internal sealed record EmailMessage(
    string RecipientEmail,
    string Subject,
    string HtmlBody);

internal static class EmailMessageFactory
{
    public static EmailMessage CreateUserInvitation(
        string recipientEmail,
        string recipientName,
        string organizationName,
        string acceptanceUrl,
        DateTime expiresAtUtc)
    {
        var encoder = HtmlEncoder.Default;
        var safeName = encoder.Encode(recipientName);
        var safeOrganization =
            encoder.Encode(organizationName);
        var safeUrl = encoder.Encode(acceptanceUrl);

        return new EmailMessage(
            recipientEmail,
            $"Invitation to {organizationName}",
            $"<p>Hello {safeName},</p>" +
            "<p>You have been invited to join " +
            $"{safeOrganization} in the Corporate " +
            "Treasury Platform.</p>" +
            $"<p><a href=\"{safeUrl}\">Accept " +
            "invitation</a></p>" +
            "<p>This invitation expires at " +
            $"{expiresAtUtc:yyyy-MM-dd HH:mm} UTC.</p>");
    }

    public static EmailMessage CreatePasswordReset(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc)
    {
        var encoder = HtmlEncoder.Default;
        var safeName = encoder.Encode(recipientName);
        var safeUrl = encoder.Encode(resetUrl);

        return new EmailMessage(
            recipientEmail,
            "Reset your password",
            $"<p>Hello {safeName},</p>" +
            "<p>A password reset was requested for " +
            "your Corporate Treasury Platform " +
            "account.</p>" +
            $"<p><a href=\"{safeUrl}\">Reset " +
            "password</a></p>" +
            "<p>This link expires at " +
            $"{expiresAtUtc:yyyy-MM-dd HH:mm} UTC.</p>" +
            "<p>If you did not request this, you can " +
            "ignore this email.</p>");
    }
}
