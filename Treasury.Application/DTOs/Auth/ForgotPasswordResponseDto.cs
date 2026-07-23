namespace Treasury.Application.DTOs.Auth;

public class ForgotPasswordResponseDto
{
    public string Message { get; set; } =
        "If the account is eligible, a password " +
        "reset email will be sent.";
}
