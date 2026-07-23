namespace Treasury.Shared.Constants;

/*
 * These values identify the organization created for
 * installations that already contain users and treasury data.
 * They provide a safe bridge from the original single-company
 * model to the new multi-organization model.
 */
public static class OrganizationDefaults
{
    public const string OrganizationCode = "DEFAULT";

    public const string OrganizationName =
        "Default Organization";

    public const string OrganizationSlug =
        "default-organization";

    public const string LegalEntityCode =
        "DEFAULT-LE";

    public const string LegalEntityName =
        "Default Legal Entity";

    public const string BusinessUnitCode =
        "HEAD-OFFICE";

    public const string BusinessUnitName =
        "Head Office";

    public const string CountryCode = "NG";

    public const string BaseCurrency = "NGN";
}
