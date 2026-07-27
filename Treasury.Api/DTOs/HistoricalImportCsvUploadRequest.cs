using Microsoft.AspNetCore.Mvc;

namespace Treasury.Api.DTOs;

public class HistoricalImportCsvUploadRequest
{
    [FromForm(Name = "mode")]
    public string Mode { get; set; } = string.Empty;

    [FromForm(Name = "file")]
    public IFormFile? File { get; set; }
}
