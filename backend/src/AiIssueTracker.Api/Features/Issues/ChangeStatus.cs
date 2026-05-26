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

namespace AiIssueTracker.Api.Features.Issues;

public static class ChangeStatus
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPatch("/api/projects/{slug}/issues/{number}/status", async (
                    string slug,
                    int number,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug, number, body.Status), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues")
                .WithName("ChangeIssueStatus");
        }

        public record Body(string Status);
    }

    public record Request(string Slug, int Number, string Status) : IRequest<IssueFull>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Status)
                .Must(IssueMapping.IsValidStatus).WithErrorCode("issues:issue:status:invalid")
                .WithMessage("Status is invalid.");
        }
    }

    public class RequestHandler(AppDbContext db) : IRequestHandler<Request, IssueFull>
    {
        public async Task<IssueFull> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var issue = await db.Issues
                .Include(i => i.Reporter)
                .Include(i => i.Assignee)
                .Include(i => i.IssueLabels).ThenInclude(il => il.Label)
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var newStatus = IssueMapping.ParseStatus(request.Status);
            var now = DateTimeOffset.UtcNow;

            if (issue.Status != IssueStatus.Done && newStatus == IssueStatus.Done)
            {
                issue.ClosedAt = now;
            }
            else if (issue.Status == IssueStatus.Done && newStatus != IssueStatus.Done)
            {
                issue.ClosedAt = null;
            }

            issue.Status = newStatus;
            issue.UpdatedAt = now;

            await db.SaveChangesAsync(ct);

            var commentCount = await db.Comments.CountAsync(c => c.IssueId == issue.Id, ct);

            return new IssueFull(
                Id: IdEncoding.Encode(issue.Id),
                Number: issue.Number,
                DisplayKey: $"{project.Slug}_{issue.Number}",
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
                AcceptanceCriteriaAiSuggested: issue.AcceptanceCriteriaAiSuggested,
                CommentCount: commentCount,
                CreatedAt: issue.CreatedAt,
                UpdatedAt: issue.UpdatedAt,
                ClosedAt: issue.ClosedAt);
        }
    }
}
