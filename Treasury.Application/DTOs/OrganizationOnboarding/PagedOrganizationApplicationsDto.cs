namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class PagedOrganizationApplicationsDto
{
    public IReadOnlyList<OrganizationApplicationResponseDto>
        Items { get; set; } =
            Array.Empty<OrganizationApplicationResponseDto>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
