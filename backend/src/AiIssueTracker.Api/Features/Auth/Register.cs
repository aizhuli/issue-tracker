using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Auth;

public static class Register
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/auth/register", async (
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(body.Email, body.Password, body.Name);
                    var response = await sender.Send(request, ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.BffOnly)
                .WithTags("Auth")
                .WithName("RegisterUser");
        }

        public record Body(string Email, string Password, string Name);
    }

    public record Request(string Email, string Password, string Name) : IRequest<Response>;

    public record Response(string Id, string Email, string Name);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithErrorCode("auth:user:email:required")
                .EmailAddress().WithErrorCode("auth:user:email:invalid_format")
                .MaximumLength(254).WithErrorCode("auth:user:email:too_long");

            RuleFor(x => x.Password)
                .NotEmpty().WithErrorCode("auth:user:password:required")
                .MinimumLength(8).WithErrorCode("auth:user:password:too_short")
                .MaximumLength(128).WithErrorCode("auth:user:password:too_long");

            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("auth:user:name:required")
                .MaximumLength(100).WithErrorCode("auth:user:name:too_long");
        }
    }

    public class RequestHandler(AppDbContext db, IdFactory idFactory, IPasswordHashing hasher)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var emailTaken = await db.Users
                .AnyAsync(u => u.Email == normalizedEmail, ct);

            if (emailTaken)
            {
                throw new ConflictException(
                    $"A user with email '{normalizedEmail}' already exists.",
                    "auth:user:email:already_exists");
            }

            var user = new User
            {
                Id = idFactory.Create(),
                Email = normalizedEmail,
                Name = request.Name.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.Hash(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return new Response(IdEncoding.Encode(user.Id), user.Email, user.Name);
        }
    }
}
