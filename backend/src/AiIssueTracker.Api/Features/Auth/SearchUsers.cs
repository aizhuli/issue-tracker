using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Auth;

public static class SearchUsers
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/users/search", async (
                [FromQuery] string? q,
                [FromQuery] int? maxPageSize,
                ISender sender,
                CancellationToken ct) => Results.Ok(await sender.Send(new Request(q, maxPageSize), ct)))
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Auth").WithName("SearchUsers");
        }
    }

    public record Request(string? Q, int? MaxPageSize) : IRequest<UserSearchResult[]>;

    public record UserSearchResult(string Id, string Name, string Email, string? AvatarUrl);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Q)
                .MaximumLength(100).WithErrorCode("auth:users:search:q_too_long")
                .When(x => x.Q is not null);

            RuleFor(x => x.MaxPageSize)
                .InclusiveBetween(1, 20).WithErrorCode("auth:users:search:max_page_size_invalid")
                .When(x => x.MaxPageSize is not null);
        }
    }

    public class RequestHandler(AppDbContext db) : IRequestHandler<Request, UserSearchResult[]>
    {
        public async Task<UserSearchResult[]> Handle(Request request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Q))
                return [];

            var limit = Math.Min(request.MaxPageSize ?? 10, 20);
            var pattern = request.Q.Trim();

            return await db.Users
                .Where(u => EF.Functions.ILike(u.Name, pattern + "%") || EF.Functions.ILike(u.Email, pattern + "%"))
                .Take(limit)
                .Select(u => new UserSearchResult(IdEncoding.Encode(u.Id), u.Name, u.Email, u.Avatar))
                .ToArrayAsync(ct);
        }
    }
}
