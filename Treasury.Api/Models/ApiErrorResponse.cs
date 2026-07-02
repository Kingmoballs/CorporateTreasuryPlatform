namespace Treasury.Api.Models;

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;

    public string Code { get; set; }
        = string.Empty;

    public string Message { get; set; }
        = string.Empty;

    public string TraceId { get; set; }
        = string.Empty;

    public IReadOnlyDictionary<string, string[]>?
        Errors { get; set; }
}