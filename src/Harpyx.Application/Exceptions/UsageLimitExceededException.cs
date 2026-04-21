namespace Harpyx.Application.Exceptions;

public class UsageLimitExceededException : InvalidOperationException
{
    public UsageLimitExceededException(string message) : base(message)
    {
    }
}
