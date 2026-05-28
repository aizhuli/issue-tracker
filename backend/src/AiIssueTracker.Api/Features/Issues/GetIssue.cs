using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Issues;

public static class GetIssue
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/{slug}/issues/{number}", async (
                    string slug, int number, ISender sender, CancellationToken ct) =>
                {
                    return Results.Ok(await sender.Send(new Request(slug, number), ct));
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues").WithName("GetIssue");
        }
    }

    public record Request(string Slug, int Number) : IRequest<IssueFull>;

    public class RequestHandler(AppDbContext db) : IRequestHandler<Request, IssueFull>
    {
        public async Task<IssueFull> Handle(Request request, CancellationToken ct)
        {
            var issue = await db.Issues
                .Include(i => i.Project)
                .Include(i => i.Reporter)
                .Include(i => i.Assignee)
                .Include(i => i.IssueLabels).ThenInclude(il => il.Label)
                .FirstOrDefaultAsync(i => i.Project.Slug == request.Slug && i.Number == request.Number, ct);

            if (issue is null)
            {
                throw new NotFoundException("Issue not found.", "issues:issue:not_found");
            }

            var commentCount = await db.Comments.CountAsync(c => c.IssueId == issue.Id, ct);

            return new IssueFull(
                Id: IdEncoding.Encode(issue.Id),
                Number: issue.Number,
                DisplayKey: $"{request.Slug}_{issue.Number}",
                Title: issue.Title,
                Description: issue.Description,
                Status: issue.Status.ToKebab(),
                Priority: issue.Priority.ToKebab(),
                Assignee: issue.Assignee is null ? null : new UserSummary(
                    Id: IdEncoding.Encode(issue.Assignee.Id),
                    Name: issue.Assignee.Name,
                    Email: issue.Assignee.Email,
                    AvatarUrl: issue.Assignee.Avatar),
                Reporter: new UserSummary(
                    Id: IdEncoding.Encode(issue.Reporter.Id),
                    Name: issue.Reporter.Name,
                    Email: issue.Reporter.Email,
                    AvatarUrl: issue.Reporter.Avatar),
                Labels: issue.IssueLabels
                    .Select(il => new LabelSummary(
                        Id: IdEncoding.Encode(il.Label.Id),
                        Name: il.Label.Name,
                        Color: il.Label.Color))
                    .ToArray(),
                AcceptanceCriteria: issue.AcceptanceCriteria,
                CommentCount: commentCount,
                CreatedAt: issue.CreatedAt,
                UpdatedAt: issue.UpdatedAt,
                ClosedAt: issue.ClosedAt);
        }
    }
}
