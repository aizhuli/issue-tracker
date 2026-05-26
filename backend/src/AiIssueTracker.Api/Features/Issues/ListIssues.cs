using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Issues;

public static class ListIssues
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/{slug}/issues", async (
                    string slug,
                    [FromQuery] string? status,
                    [FromQuery] string? pageToken,
                    [FromQuery] int? maxPageSize,
                    [FromQuery] string? q,
                    [FromQuery(Name = "priority")] string[]? priority,
                    [FromQuery(Name = "assignee")] string[]? assignee,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(
                        new Request(slug, status, pageToken, maxPageSize, q, priority, assignee), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues")
                .WithName("ListIssues");
        }
    }

    public record Request(
        string Slug,
        string? Status,
        string? PageToken,
        int? MaxPageSize,
        string? Q,
        string[]? Priorities,
        string[]? Assignees)
        : IRequest<PageResponse<IssueSummary>>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Status)
                .NotEmpty().WithErrorCode("issues:issue:status:invalid")
                .Must(IssueMapping.IsValidStatus).WithErrorCode("issues:issue:status:invalid");

            RuleForEach(x => x.Priorities)
                .Must(IssueMapping.IsValidPriority).WithErrorCode("issues:issue:priority:invalid")
                .When(x => x.Priorities is { Length: > 0 });
        }
    }

    public class RequestHandler(AppDbContext db, LimitOffsetPaging paging)
        : IRequestHandler<Request, PageResponse<IssueSummary>>
    {
        public async Task<PageResponse<IssueSummary>> Handle(Request request, CancellationToken ct)
        {
            if (!paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
                throw PaginationExceptions.InvalidMaxPageSize();

            if (!paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit,
                    request.Status, request.Q,
                    string.Join(",", request.Priorities ?? []),
                    string.Join(",", request.Assignees ?? [])))
                throw PaginationExceptions.InvalidPageToken();

            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var parsedStatus = IssueMapping.ParseStatus(request.Status!);

            var query = db.Issues
                .AsNoTracking()
                .Where(i => i.ProjectId == project.Id && i.Status == parsedStatus)
                .Include(i => i.Assignee)
                .Include(i => i.IssueLabels)
                    .ThenInclude(il => il.Label)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q;
                query = query.Where(i =>
                    EF.Functions.ILike(i.Title, $"%{q}%") ||
                    EF.Functions.ILike(i.Description ?? "", $"%{q}%"));
            }

            if (request.Priorities is { Length: > 0 })
            {
                var priorities = request.Priorities
                    .Select(IssueMapping.ParsePriority)
                    .ToList();
                query = query.Where(i => priorities.Contains(i.Priority));
            }

            if (request.Assignees is { Length: > 0 })
            {
                var unassignedIncluded = request.Assignees.Contains("unassigned", StringComparer.OrdinalIgnoreCase);
                var assigneeIds = request.Assignees
                    .Where(a => !string.Equals(a, "unassigned", StringComparison.OrdinalIgnoreCase))
                    .Select(a => IdEncoding.TryDecode(a, out var id) ? (long?)id : null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                query = query.Where(i =>
                    (assigneeIds.Count > 0 && i.AssigneeId.HasValue && assigneeIds.Contains(i.AssigneeId.Value)) ||
                    (!i.AssigneeId.HasValue && unassignedIncluded));
            }

            var slug = project.Slug;

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Skip(offset!.Value)
                .Take(limit!.Value)
                .Select(i => new IssueSummary(
                    IdEncoding.Encode(i.Id),
                    i.Number,
                    slug + "_" + i.Number,
                    i.Title,
                    i.Status.ToKebab(),
                    i.Priority.ToKebab(),
                    i.Assignee == null
                        ? null
                        : new UserSummary(
                            IdEncoding.Encode(i.Assignee.Id),
                            i.Assignee.Name,
                            i.Assignee.Email,
                            i.Assignee.Avatar),
                    i.IssueLabels
                        .Select(il => new LabelSummary(
                            IdEncoding.Encode(il.Label.Id),
                            il.Label.Name,
                            il.Label.Color))
                        .ToArray(),
                    db.Comments.Count(c => c.IssueId == i.Id),
                    i.UpdatedAt))
                .ToListAsync(ct);

            var nextToken = paging.CreateNextPageToken(
                items.Count, offset!.Value, limit!.Value,
                request.Status, request.Q,
                string.Join(",", request.Priorities ?? []),
                string.Join(",", request.Assignees ?? []));

            return new PageResponse<IssueSummary>(items.ToArray(), nextToken);
        }
    }
}
