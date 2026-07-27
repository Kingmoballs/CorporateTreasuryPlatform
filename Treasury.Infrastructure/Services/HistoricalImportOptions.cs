namespace Treasury.Infrastructure.Services;

public class HistoricalImportOptions
{
    public const string SectionName =
        "HistoricalImports";

    public int MaximumFileSizeBytes { get; set; } =
        5 * 1024 * 1024;

    public int MaximumRowCount { get; set; } =
        10_000;
}
