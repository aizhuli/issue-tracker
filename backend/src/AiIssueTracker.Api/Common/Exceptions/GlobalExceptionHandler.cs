using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AiIssueTracker.Api.Common.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problem = exception switch
        {
            ValidationException ve => BuildValidationProblem(ve, traceId),
            DomainException de => BuildDomainProblem(de, traceId),
            _ => BuildUnexpectedProblem(exception, traceId)
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        }
        else
        {
            logger.LogInformation(
                "Handled domain exception {ErrorCode} -> {Status}. TraceId: {TraceId}",
                problem.Extensions["errorCode"], problem.Status, traceId);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails BuildValidationProblem(ValidationException exception, string traceId)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => string.IsNullOrEmpty(g.Key) ? "_" : char.ToLowerInvariant(g.Key[0]) + g.Key[1..],
                g => g.Select(e => new { code = e.ErrorCode, message = e.ErrorMessage }).Cast<object>().ToArray());

        var problem = new ProblemDetails
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc7807",
        };
        problem.Extensions["errorCode"] = "common:validation:failed";
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errors"] = errors;
        return problem;
    }

    private static ProblemDetails BuildDomainProblem(DomainException exception, string traceId)
    {
        var problem = new ProblemDetails
        {
            Title = ReasonPhrases.For(exception.StatusCode),
            Status = exception.StatusCode,
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7807",
        };
        problem.Extensions["errorCode"] = exception.ErrorCode;
        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    private static ProblemDetails BuildUnexpectedProblem(Exception exception, string traceId)
    {
        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred.",
            Type = "https://tools.ietf.org/html/rfc7807",
        };
        problem.Extensions["errorCode"] = "common:server:unexpected";
        problem.Extensions["traceId"] = traceId;
        return problem;
    }
}

file static class ReasonPhrases
{
    public static string For(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        502 => "Bad Gateway",
        _ => "Error"
    };
}
