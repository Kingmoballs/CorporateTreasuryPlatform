using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailDeliveryOptions _options;

    public SmtpEmailSender(
        IOptions<EmailDeliveryOptions> options)
    {
        _options = options.Value;
    }

    public void EnsureConfigured()
    {
        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(
                _options.Host) ||
            string.IsNullOrWhiteSpace(
                _options.FromAddress))
        {
            throw new BusinessRuleException(
                "Email delivery is not configured. " +
                "Enable EmailDelivery and provide SMTP " +
                "settings before sending account emails.");
        }
    }

    public async Task SendUserInvitation(
        string recipientEmail,
        string recipientName,
        string organizationName,
        string acceptanceUrl,
        DateTime expiresAtUtc)
    {
        EnsureConfigured();

        var encoder = HtmlEncoder.Default;

        var safeName =
            encoder.Encode(recipientName);

        var safeOrganization =
            encoder.Encode(organizationName);

        var safeUrl =
            encoder.Encode(acceptanceUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromAddress,
                _options.FromName),
            Subject =
                $"Invitation to {organizationName}",
            IsBodyHtml = true,
            Body =
                $"<p>Hello {safeName},</p>" +
                $"<p>You have been invited to join " +
                $"{safeOrganization} in the Corporate " +
                "Treasury Platform.</p>" +
                $"<p><a href=\"{safeUrl}\">Accept " +
                "invitation</a></p>" +
                $"<p>This invitation expires at " +
                $"{expiresAtUtc:yyyy-MM-dd HH:mm} UTC." +
                "</p>"
        };

        message.To.Add(recipientEmail);

        using var client =
            new SmtpClient(
                _options.Host,
                _options.Port)
            {
                EnableSsl = _options.UseSsl
            };

        if (!string.IsNullOrWhiteSpace(
                _options.Username))
        {
            client.Credentials =
                new NetworkCredential(
                    _options.Username,
                    _options.Password);
        }

        await client.SendMailAsync(message);
    }

    public async Task SendPasswordReset(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc)
    {
        EnsureConfigured();

        var encoder = HtmlEncoder.Default;

        var safeName =
            encoder.Encode(recipientName);

        var safeUrl =
            encoder.Encode(resetUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromAddress,
                _options.FromName),
            Subject = "Reset your password",
            IsBodyHtml = true,
            Body =
                $"<p>Hello {safeName},</p>" +
                "<p>A password reset was requested for " +
                "your Corporate Treasury Platform " +
                "account.</p>" +
                $"<p><a href=\"{safeUrl}\">Reset " +
                "password</a></p>" +
                $"<p>This link expires at " +
                $"{expiresAtUtc:yyyy-MM-dd HH:mm} UTC." +
                "</p>" +
                "<p>If you did not request this, you can " +
                "ignore this email.</p>"
        };

        message.To.Add(recipientEmail);

        using var client =
            new SmtpClient(
                _options.Host,
                _options.Port)
            {
                EnableSsl = _options.UseSsl
            };

        if (!string.IsNullOrWhiteSpace(
                _options.Username))
        {
            client.Credentials =
                new NetworkCredential(
                    _options.Username,
                    _options.Password);
        }

        await client.SendMailAsync(message);
    }
}
