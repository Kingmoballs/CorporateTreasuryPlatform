namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class OrganizationApplicationQueryDto
{
    public string? Status { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
