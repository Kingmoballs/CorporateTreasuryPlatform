public interface ICurrentUserService
{
    Guid UserId { get; }

    string Email { get; }

    string Role { get; }

    Guid? OrganizationId { get; }

    Guid? OrganizationMembershipId { get; }

    Guid? AuthenticationSessionId { get; }

    string OrganizationCode { get; }
}
