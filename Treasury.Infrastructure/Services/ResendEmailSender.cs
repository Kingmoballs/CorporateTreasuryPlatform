using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Services;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<EmailDeliveryOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled &&
        _options.Provider ==
            EmailDeliveryProvider.Resend &&
        !string.IsNullOrWhiteSpace(
            _options.ResendApiKey) &&
        !string.IsNullOrWhiteSpace(
            _options.FromAddress) &&
        TryGetApiBaseUri(out _);

    public void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new BusinessRuleException(
                "Email delivery is not configured. " +
                "Enable the Resend email provider and " +
                "provide its API settings before " +
                "sending account emails.");
        }
    }

    public Task SendUserInvitation(
        string recipientEmail,
        string recipientName,
        string organizationName,
        string acceptanceUrl,
        DateTime expiresAtUtc)
    {
        EnsureConfigured();

        return Send(
            EmailMessageFactory.CreateUserInvitation(
                recipientEmail,
                recipientName,
                organizationName,
                acceptanceUrl,
                expiresAtUtc));
    }

    public Task SendPasswordReset(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        DateTime expiresAtUtc)
    {
        EnsureConfigured();

        return Send(
            EmailMessageFactory.CreatePasswordReset(
                recipientEmail,
                recipientName,
                resetUrl,
                expiresAtUtc));
    }

    private async Task Send(EmailMessage email)
    {
        TryGetApiBaseUri(out var apiBaseUri);
        var endpoint = new Uri(apiBaseUri!, "emails");
        var from = new MailAddress(
                _options.FromAddress,
                _options.FromName)
            .ToString();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.ResendApiKey.Trim());
        request.Headers.UserAgent.ParseAdd(
            "CorporateTreasuryPlatform/1.0");
        request.Content = JsonContent.Create(
            new ResendEmailRequest(
                from,
                new[] { email.RecipientEmail },
                email.Subject,
                email.HtmlBody));

        using var response = await _httpClient.SendAsync(
            request);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _logger.LogError(
            "Resend rejected an account email with " +
            "HTTP status {StatusCode}.",
            (int)response.StatusCode);

        throw new HttpRequestException(
            "The email provider rejected the message.",
            null,
            response.StatusCode);
    }

    private bool TryGetApiBaseUri(out Uri? uri)
    {
        var value = _options.ResendApiBaseUrl.Trim();

        if (!value.EndsWith('/'))
        {
            value += "/";
        }

        return Uri.TryCreate(
                value,
                UriKind.Absolute,
                out uri) &&
            uri.Scheme == Uri.UriSchemeHttps;
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")]
        string From,
        [property: JsonPropertyName("to")]
        string[] To,
        [property: JsonPropertyName("subject")]
        string Subject,
        [property: JsonPropertyName("html")]
        string Html);
}
