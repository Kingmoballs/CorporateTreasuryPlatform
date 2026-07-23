namespace Treasury.Application.DTOs.Auth;

public class StartMfaEnrollmentDto
{
    public string CurrentPassword { get; set; } =
        string.Empty;
}
