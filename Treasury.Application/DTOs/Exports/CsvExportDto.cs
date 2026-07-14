namespace Treasury.Application.DTOs.Exports;

public class CsvExportDto
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "text/csv";

    public byte[] Content { get; set; } = Array.Empty<byte>();
}