using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects;

public static class ListProjects
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects", async (
                    [FromQuery] string? pageToken,
                    [FromQuery] int? maxPageSize,
                    [FromQuery] string? q,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(pageToken, maxPageSize, q), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("ListProjects");
        }
    }

    public record Request(string? PageToken, int? MaxPageSize, string? Q)
        : IRequest<PageResponse<ProjectSummary>>;

    public record ProjectSummary(
        string Id,
        string Slug,
        string Name,
        string OwnerId,
        string OwnerName,
        DateTimeOffset CreatedAt);

    public class RequestHandler(AppDbContext db, LimitOffsetPaging paging)
        : IRequestHandler<Request, PageResponse<ProjectSummary>>
    {
        public async Task<PageResponse<ProjectSummary>> Handle(Request request, CancellationToken ct)
        {
            if (!paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
                throw PaginationExceptions.InvalidMaxPageSize();

            if (!paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit, request.Q))
                throw PaginationExceptions.InvalidPageToken();

            var query = db.Projects.Include(p => p.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var prefix = request.Q;
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, prefix + "%") ||
                    EF.Functions.ILike(p.Slug, prefix + "%"));
            }

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip(offset!.Value)
                .Take(limit!.Value)
                .Select(p => new ProjectSummary(
                    IdEncoding.Encode(p.Id),
                    p.Slug,
                    p.Name,
                    IdEncoding.Encode(p.OwnerId),
                    p.Owner.Name,
                    p.CreatedAt))
                .ToListAsync(ct);

            var nextToken = paging.CreateNextPageToken(items.Count, offset!.Value, limit!.Value, request.Q);

            return new PageResponse<ProjectSummary>(items.ToArray(), nextToken);
        }
    }
}
