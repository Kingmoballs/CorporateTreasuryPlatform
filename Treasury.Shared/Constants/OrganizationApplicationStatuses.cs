namespace Treasury.Shared.Constants;

public static class OrganizationApplicationStatuses
{
    public const string Submitted = "Submitted";

    public const string UnderReview = "UnderReview";

    public const string Approved = "Approved";

    public const string Rejected = "Rejected";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(
            new[]
            {
                Submitted,
                UnderReview,
                Approved,
                Rejected
            },
            StringComparer.OrdinalIgnoreCase);
}
