namespace AiIssueTracker.Api.Common.Exceptions;

public abstract class DomainException(string message, string errorCode, int statusCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status404NotFound);

public sealed class ConflictException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status409Conflict);

public sealed class UnauthorizedDomainException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status401Unauthorized);

public sealed class ForbiddenDomainException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status403Forbidden);

public sealed class UnprocessableEntityException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status422UnprocessableEntity);

public sealed class BadGatewayException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status502BadGateway);
