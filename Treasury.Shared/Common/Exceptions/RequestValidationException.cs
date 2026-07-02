namespace Treasury.Application.Common.Exceptions;

public class RequestValidationException
    : Exception
{
    public RequestValidationException(
        string message)
        : base(message)
    {
    }
}