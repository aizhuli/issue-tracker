namespace AiIssueTracker.Api.Common.Exceptions;

public sealed class BadRequestException(string message, string errorCode)
    : DomainException(message, errorCode, StatusCodes.Status400BadRequest);
