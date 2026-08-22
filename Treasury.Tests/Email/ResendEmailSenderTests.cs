using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Treasury.Application.Common.Exceptions;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Email;

public class ResendEmailSenderTests
{
    [Fact]
    public async Task
        SendInvitation_UsesAuthenticatedHttpsApi()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK);
        var sender = CreateSender(handler);

        await sender.SendUserInvitation(
            "user@example.com",
            "Ada <Admin>",
            "Example & Co",
            "https://app.example.com/accept?token=a&b=c",
            new DateTime(
                2030,
                1,
                2,
                3,
                4,
                0,
                DateTimeKind.Utc));

        Assert.Equal(
            "https://api.resend.com/emails",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthScheme);
        Assert.Equal("re_test_key", handler.AuthParameter);
        Assert.Contains(
            "CorporateTreasuryPlatform/1.0",
            handler.UserAgent);

        using var payload = JsonDocument.Parse(
            handler.Content!);
        var root = payload.RootElement;

        Assert.Equal(
            "\"Treasury Mail\" <no-reply@example.com>",
            root.GetProperty("from").GetString());
        Assert.Equal(
            "user@example.com",
            root.GetProperty("to")[0].GetString());
        Assert.Equal(
            "Invitation to Example & Co",
            root.GetProperty("subject").GetString());

        var html = root.GetProperty("html").GetString();
        Assert.Contains("Ada &lt;Admin&gt;", html);
        Assert.Contains("Example &amp; Co", html);
        Assert.Contains("a&amp;b=c", html);
    }

    [Fact]
    public void EnsureConfigured_RejectsMissingApiKey()
    {
        var sender = CreateSender(
            new RecordingHandler(HttpStatusCode.OK),
            apiKey: string.Empty);

        var exception = Assert.Throws<
            BusinessRuleException>(
                sender.EnsureConfigured);

        Assert.Contains(
            "Resend",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendPasswordReset_RejectsProviderError()
    {
        var sender = CreateSender(
            new RecordingHandler(
                HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<
            HttpRequestException>(
                () => sender.SendPasswordReset(
                    "user@example.com",
                    "Ada",
                    "https://app.example.com/reset",
                    DateTime.UtcNow.AddMinutes(30)));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            exception.StatusCode);
        Assert.DoesNotContain(
            "re_test_key",
            exception.Message);
    }

    private static ResendEmailSender CreateSender(
        HttpMessageHandler handler,
        string apiKey = "re_test_key")
    {
        var options = Options.Create(
            new EmailDeliveryOptions
            {
                Enabled = true,
                Provider = EmailDeliveryProvider.Resend,
                ResendApiKey = apiKey,
                FromAddress = "no-reply@example.com",
                FromName = "Treasury Mail"
            });

        return new ResendEmailSender(
            new HttpClient(handler),
            options,
            NullLogger<ResendEmailSender>.Instance);
    }

    private sealed class RecordingHandler :
        HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public Uri? RequestUri { get; private set; }

        public string? AuthScheme { get; private set; }

        public string? AuthParameter { get; private set; }

        public string UserAgent { get; private set; } =
            string.Empty;

        public string? Content { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthScheme =
                request.Headers.Authorization?.Scheme;
            AuthParameter =
                request.Headers.Authorization?.Parameter;
            UserAgent = request.Headers.UserAgent.ToString();
            Content = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}
