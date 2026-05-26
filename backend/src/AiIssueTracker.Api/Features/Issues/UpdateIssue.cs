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

public static class UpdateIssue
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("/api/projects/{slug}/issues/{number}", async (
                    string slug,
                    int number,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(
                        slug,
                        number,
                        body.Title,
                        body.Description,
                        body.Status,
                        body.Priority,
                        body.AssigneeId,
                        body.LabelIds,
                        body.AcceptanceCriteria);
                    return Results.Ok(await sender.Send(request, ct));
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues")
                .WithName("UpdateIssue");
        }

        public record Body(
            string Title,
            string? Description,
            string Status,
            string Priority,
            string? AssigneeId,
            string[]? LabelIds,
            string? AcceptanceCriteria);
    }

    public record Request(
        string Slug,
        int Number,
        string Title,
        string? Description,
        string Status,
        string Priority,
        string? AssigneeId,
        string[]? LabelIds,
        string? AcceptanceCriteria) : IRequest<IssueFull>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithErrorCode("issues:issue:title:required_or_too_long")
                .MaximumLength(200).WithErrorCode("issues:issue:title:required_or_too_long");

            RuleFor(x => x.Description)
                .MaximumLength(10000).WithErrorCode("issues:issue:description:too_long")
                .When(x => x.Description is not null);

            RuleFor(x => x.AcceptanceCriteria)
                .MaximumLength(10000).WithErrorCode("issues:issue:acceptance_criteria:too_long")
                .When(x => x.AcceptanceCriteria is not null);

            RuleFor(x => x.Status)
                .Must(IssueMapping.IsValidStatus).WithErrorCode("issues:issue:status:invalid");

            RuleFor(x => x.Priority)
                .Must(IssueMapping.IsValidPriority).WithErrorCode("issues:issue:priority:invalid");

            RuleFor(x => x.LabelIds)
                .Must(ids => ids!.Length <= 20).WithErrorCode("issues:issue:labels:too_many")
                .When(x => x.LabelIds is not null);
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
                .Include(i => i.Project)
                .Include(i => i.Reporter)
                .Include(i => i.Assignee)
                .Include(i => i.IssueLabels).ThenInclude(il => il.Label)
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var now = DateTimeOffset.UtcNow;
            var oldStatus = issue.Status;
            var newStatus = IssueMapping.ParseStatus(request.Status);
            var newPriority = IssueMapping.ParsePriority(request.Priority);

            long? assigneeId = null;
            if (request.AssigneeId is not null)
            {
                if (!IdEncoding.TryDecode(request.AssigneeId, out var decodedAssigneeId))
                    throw new NotFoundException("Assignee not found.", "issues:issue:assignee:not_found");

                var assigneeExists = await db.Users.AnyAsync(u => u.Id == decodedAssigneeId, ct);
                if (!assigneeExists)
                    throw new NotFoundException("Assignee not found.", "issues:issue:assignee:not_found");

                assigneeId = decodedAssigneeId;
            }

            var newLabelIds = new List<long>();
            if (request.LabelIds is { Length: > 0 })
            {
                foreach (var encodedId in request.LabelIds)
                {
                    if (!IdEncoding.TryDecode(encodedId, out var labelId))
                        throw new BadRequestException("One or more labels not in project.", "issues:issue:labels:not_in_project");
                    newLabelIds.Add(labelId);
                }

                var projectLabels = await db.Labels
                    .Where(l => l.ProjectId == project.Id && newLabelIds.Contains(l.Id))
                    .ToListAsync(ct);

                if (projectLabels.Count != newLabelIds.Count)
                    throw new BadRequestException("One or more labels not in project.", "issues:issue:labels:not_in_project");
            }

            issue.Title = request.Title.Trim();
            issue.Description = request.Description?.Trim();
            issue.Status = newStatus;
            issue.Priority = newPriority;
            issue.AssigneeId = assigneeId;
            issue.AcceptanceCriteria = request.AcceptanceCriteria?.Trim();
            issue.UpdatedAt = now;

            if (oldStatus != IssueStatus.Done && newStatus == IssueStatus.Done)
                issue.ClosedAt = now;
            else if (oldStatus == IssueStatus.Done && newStatus != IssueStatus.Done)
                issue.ClosedAt = null;

            db.IssueLabels.RemoveRange(issue.IssueLabels);

            foreach (var labelId in newLabelIds)
            {
                db.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = labelId });
            }

            await db.SaveChangesAsync(ct);

            var commentCount = await db.Comments.CountAsync(c => c.IssueId == issue.Id, ct);

            var loaded = await db.Issues
                .Include(i => i.Reporter)
                .Include(i => i.Assignee)
                .Include(i => i.IssueLabels).ThenInclude(il => il.Label)
                .FirstAsync(i => i.Id == issue.Id, ct);

            return new IssueFull(
                Id: IdEncoding.Encode(loaded.Id),
                Number: loaded.Number,
                DisplayKey: $"{project.Slug}_{loaded.Number}",
                Title: loaded.Title,
                Description: loaded.Description,
                Status: loaded.Status.ToKebab(),
                Priority: loaded.Priority.ToKebab(),
                Assignee: loaded.Assignee is null ? null : new UserSummary(
                    Id: IdEncoding.Encode(loaded.Assignee.Id),
                    Name: loaded.Assignee.Name,
                    Email: loaded.Assignee.Email,
                    AvatarUrl: loaded.Assignee.Avatar),
                Reporter: new UserSummary(
                    Id: IdEncoding.Encode(loaded.Reporter.Id),
                    Name: loaded.Reporter.Name,
                    Email: loaded.Reporter.Email,
                    AvatarUrl: loaded.Reporter.Avatar),
                Labels: loaded.IssueLabels
                    .Select(il => new LabelSummary(
                        Id: IdEncoding.Encode(il.Label.Id),
                        Name: il.Label.Name,
                        Color: il.Label.Color))
                    .ToArray(),
                AcceptanceCriteria: loaded.AcceptanceCriteria,
                AcceptanceCriteriaAiSuggested: loaded.AcceptanceCriteriaAiSuggested,
                CommentCount: commentCount,
                CreatedAt: loaded.CreatedAt,
                UpdatedAt: loaded.UpdatedAt,
                ClosedAt: loaded.ClosedAt);
        }
    }
}
