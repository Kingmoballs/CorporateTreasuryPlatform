namespace Treasury.Application.DTOs.Auth;

public class StartMfaEnrollmentResponseDto
{
    public string ManualEntryKey { get; set; } =
        string.Empty;

    public string ProvisioningUri { get; set; } =
        string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}
