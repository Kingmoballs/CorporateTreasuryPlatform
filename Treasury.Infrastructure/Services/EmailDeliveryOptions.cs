namespace Treasury.Infrastructure.Services;

public class EmailDeliveryOptions
{
    public const string SectionName =
        "EmailDelivery";

    public bool Enabled { get; set; }

    public string Host { get; set; } =
        string.Empty;

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } =
        string.Empty;

    public string FromName { get; set; } =
        "Corporate Treasury Platform";
}
