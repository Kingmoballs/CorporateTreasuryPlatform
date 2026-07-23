namespace Treasury.Application.Interfaces;

/*
 * HTTP requests are tenant-scoped. Processes without an
 * HTTP context, such as migrations, seeders and background
 * workers, operate in an explicit system scope.
 */
public interface IOrganizationContext
{
    Guid? OrganizationId { get; }

    bool IsSystemScope { get; }
}
