namespace Treasury.Application.Common.Exceptions;

public class ForbiddenOperationException
    : Exception
{
    public ForbiddenOperationException(
        string message)
        : base(message)
    {
    }
}