namespace Treasury.Application.Common.Exceptions;

public class ResourceNotFoundException
    : Exception
{
    public ResourceNotFoundException(
        string message)
        : base(message)
    {
    }
}