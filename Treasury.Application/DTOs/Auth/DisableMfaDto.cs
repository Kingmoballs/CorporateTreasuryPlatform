namespace Treasury.Application.DTOs.Auth;

public class DisableMfaDto
{
    public string CurrentPassword { get; set; } =
        string.Empty;

    public string Code { get; set; } =
        string.Empty;
}
