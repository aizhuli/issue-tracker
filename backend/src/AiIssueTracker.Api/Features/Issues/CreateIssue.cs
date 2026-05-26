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

public static class CreateIssue
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects/{slug}/issues", async (
                    string slug,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(
                        slug,
                        body.Title,
                        body.Description,
                        body.Priority,
                        body.AssigneeId,
                        body.LabelIds,
                        body.AcceptanceCriteria);
                    var response = await sender.Send(request, ct);
                    return Results.Created($"/api/projects/{slug}/issues/{response.Number}", response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues")
                .WithName("CreateIssue");
        }

        public record Body(
            string Title,
            string? Description,
            string? Priority,
            string? AssigneeId,
            string[]? LabelIds,
            string? AcceptanceCriteria);
    }

    public record Request(
        string Slug,
        string Title,
        string? Description,
        string? Priority,
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

            RuleFor(x => x.Priority)
                .Must(IssueMapping.IsValidPriority).WithErrorCode("issues:issue:priority:invalid")
                .When(x => x.Priority is not null);

            RuleFor(x => x.LabelIds)
                .Must(ids => ids!.Length <= 20).WithErrorCode("issues:issue:labels:too_many")
                .When(x => x.LabelIds is not null);
        }
    }

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser, IdFactory idFactory)
        : IRequestHandler<Request, IssueFull>
    {
        public async Task<IssueFull> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var numberResult = await db.Database
                .SqlQuery<int>(
                    $"UPDATE projects SET next_issue_number = next_issue_number + 1 WHERE id = {project.Id} RETURNING next_issue_number - 1")
                .ToListAsync(ct);

            var issueNumber = numberResult[0];
            var now = DateTimeOffset.UtcNow;

            var priority = request.Priority is not null
                ? IssueMapping.ParsePriority(request.Priority)
                : IssuePriority.Medium;

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

            var issue = new Issue
            {
                Id = idFactory.Create(),
                ProjectId = project.Id,
                Number = issueNumber,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                Status = IssueStatus.Backlog,
                Priority = priority,
                AssigneeId = assigneeId,
                ReporterId = currentUser.UserId,
                AcceptanceCriteria = request.AcceptanceCriteria?.Trim(),
                AcceptanceCriteriaAiSuggested = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Issues.Add(issue);

            if (request.LabelIds is { Length: > 0 })
            {
                var decodedLabelIds = new List<long>();
                foreach (var encodedId in request.LabelIds)
                {
                    if (!IdEncoding.TryDecode(encodedId, out var labelId))
                        throw new BadRequestException("One or more labels not in project.", "issues:issue:labels:not_in_project");
                    decodedLabelIds.Add(labelId);
                }

                var projectLabels = await db.Labels
                    .Where(l => l.ProjectId == project.Id && decodedLabelIds.Contains(l.Id))
                    .ToListAsync(ct);

                if (projectLabels.Count != decodedLabelIds.Count)
                    throw new BadRequestException("One or more labels not in project.", "issues:issue:labels:not_in_project");

                foreach (var label in projectLabels)
                {
                    db.IssueLabels.Add(new IssueLabel { IssueId = issue.Id, LabelId = label.Id });
                }
            }

            await db.SaveChangesAsync(ct);

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
                    IdEncoding.Encode(loaded.Assignee.Id),
                    loaded.Assignee.Name,
                    loaded.Assignee.Email,
                    loaded.Assignee.Avatar),
                Reporter: new UserSummary(
                    IdEncoding.Encode(loaded.Reporter.Id),
                    loaded.Reporter.Name,
                    loaded.Reporter.Email,
                    loaded.Reporter.Avatar),
                Labels: loaded.IssueLabels
                    .Select(il => new LabelSummary(
                        IdEncoding.Encode(il.Label.Id),
                        il.Label.Name,
                        il.Label.Color))
                    .ToArray(),
                AcceptanceCriteria: loaded.AcceptanceCriteria,
                AcceptanceCriteriaAiSuggested: loaded.AcceptanceCriteriaAiSuggested,
                CommentCount: 0,
                CreatedAt: loaded.CreatedAt,
                UpdatedAt: loaded.UpdatedAt,
                ClosedAt: loaded.ClosedAt);
        }
    }
}
