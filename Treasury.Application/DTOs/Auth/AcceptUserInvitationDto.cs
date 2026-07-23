namespace Treasury.Application.DTOs.Auth;

public class AcceptUserInvitationDto
{
    public string Token { get; set; } = string.Empty;

    /*
     * A password is required when the invitation creates a
     * new account. Existing users retain their password.
     */
    public string? Password { get; set; }
}
