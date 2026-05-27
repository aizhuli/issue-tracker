using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Comments;

public record CommentDto(
    string Id,
    string AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool Edited);

public static class ListComments
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/{slug}/issues/{number}/comments", async (
                    string slug,
                    int number,
                    [Microsoft.AspNetCore.Mvc.FromQuery] string? pageToken,
                    [Microsoft.AspNetCore.Mvc.FromQuery] int? maxPageSize,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug, number, pageToken, maxPageSize), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Comments")
                .WithName("ListComments");
        }
    }

    public record Request(
        string Slug,
        int Number,
        string? PageToken,
        int? MaxPageSize)
        : IRequest<PageResponse<CommentDto>>;

    public class RequestHandler(AppDbContext db, LimitOffsetPaging paging)
        : IRequestHandler<Request, PageResponse<CommentDto>>
    {
        public async Task<PageResponse<CommentDto>> Handle(Request request, CancellationToken ct)
        {
            if (!paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
                throw PaginationExceptions.InvalidMaxPageSize();

            if (!paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit,
                    request.Slug, request.Number))
                throw PaginationExceptions.InvalidPageToken();

            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var items = await db.Comments
                .AsNoTracking()
                .Where(c => c.IssueId == issue.Id)
                .Include(c => c.Author)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Skip(offset!.Value)
                .Take(limit!.Value)
                .Select(c => new CommentDto(
                    IdEncoding.Encode(c.Id),
                    IdEncoding.Encode(c.AuthorId),
                    c.Author.Name,
                    c.Author.Avatar,
                    c.Body,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.UpdatedAt > c.CreatedAt.AddSeconds(1)))
                .ToListAsync(ct);

            var nextToken = paging.CreateNextPageToken(
                items.Count, offset!.Value, limit!.Value,
                request.Slug, request.Number);

            return new PageResponse<CommentDto>(items.ToArray(), nextToken);
        }
    }
}
