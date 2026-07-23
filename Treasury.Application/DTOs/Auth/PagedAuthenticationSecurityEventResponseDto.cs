namespace Treasury.Application.DTOs.Auth;

public class PagedAuthenticationSecurityEventResponseDto
{
    public IReadOnlyList<
        AuthenticationSecurityEventResponseDto>
        Items { get; set; } =
            Array.Empty<
                AuthenticationSecurityEventResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
