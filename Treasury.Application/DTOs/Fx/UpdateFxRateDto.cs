namespace Treasury.Application.DTOs.Fx;

public class UpdateFxRateDto
{
    public decimal Rate { get; set; }

    public string SourceType { get; set; } = "Manual";

    public string? SourceReference { get; set; }

    public bool IsActive { get; set; } = true;
}