using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Auth;

public static class Me
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/auth/me", async (
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Auth")
                .WithName("GetCurrentUser");
        }
    }

    public record Request : IRequest<Response>;

    public record Response(string Id, string Email, string Name, string? Avatar);

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var userId = currentUser.UserId;

            var user = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new Response(IdEncoding.Encode(u.Id), u.Email, u.Name, u.Avatar))
                .FirstOrDefaultAsync(ct);

            if (user is null)
            {
                throw new NotFoundException(
                    "Current user not found.",
                    "auth:user:not_found");
            }

            return user;
        }
    }
}
