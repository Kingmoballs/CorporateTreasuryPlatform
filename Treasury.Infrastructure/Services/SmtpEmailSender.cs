using System.Net;
using System.Net.Mail;
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

    public bool IsConfigured =>
        _options.Enabled &&
        _options.Provider ==
            EmailDeliveryProvider.Smtp &&
        !string.IsNullOrWhiteSpace(
            _options.Host) &&
        !string.IsNullOrWhiteSpace(
            _options.FromAddress);

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new BusinessRuleException(
                "Email delivery is not configured. " +
                "Enable the SMTP email provider and " +
                "provide its settings before sending " +
                "account emails.");
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

        await Send(
            EmailMessageFactory.CreateUserInvitation(
                recipientEmail,
                recipientName,
                organizationName,
                acceptanceUrl,
                expiresAtUtc));
    }

    public async Task SendPasswordReset(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc)
    {
        EnsureConfigured();

        await Send(
            EmailMessageFactory.CreatePasswordReset(
                recipientEmail,
                recipientName,
                resetUrl,
                expiresAtUtc));
    }

    private async Task Send(EmailMessage email)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(
                _options.FromAddress,
                _options.FromName),
            Subject = email.Subject,
            IsBodyHtml = true,
            Body = email.HtmlBody
        };

        message.To.Add(email.RecipientEmail);

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
