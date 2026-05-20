using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Auth;

public static class Login
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/auth/login", async (
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(body.Email, body.Password);
                    var response = await sender.Send(request, ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.BffOnly)
                .WithTags("Auth")
                .WithName("LoginUser");
        }

        public record Body(string Email, string Password);
    }

    public record Request(string Email, string Password) : IRequest<Response>;

    public record Response(string Id, string Email, string Name);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithErrorCode("auth:user:email:required")
                .EmailAddress().WithErrorCode("auth:user:email:invalid_format");

            RuleFor(x => x.Password)
                .NotEmpty().WithErrorCode("auth:user:password:required");
        }
    }

    public class RequestHandler(AppDbContext db, IPasswordHashing hasher)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

            if (user is null || !hasher.Verify(user, user.PasswordHash, request.Password))
            {
                throw new UnauthorizedDomainException(
                    "Invalid email or password.",
                    "auth:credentials:invalid");
            }

            return new Response(IdEncoding.Encode(user.Id), user.Email, user.Name);
        }
    }
}
