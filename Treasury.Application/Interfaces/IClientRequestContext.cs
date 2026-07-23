namespace Treasury.Application.Interfaces;

public interface IClientRequestContext
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}
