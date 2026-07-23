namespace Treasury.Application.DTOs.Auth;

public class MfaRecoveryCodesResponseDto
{
    public IReadOnlyList<string> RecoveryCodes
        { get; set; } = Array.Empty<string>();
}
