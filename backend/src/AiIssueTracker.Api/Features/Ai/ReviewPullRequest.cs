using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Integrations.GitHub;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AiIssueTracker.Api.Features.Ai;

public static class ReviewPullRequest
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects/{slug}/issues/{number}/ai/review-pr", async (
                    string slug,
                    int number,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(slug, number, body.PullRequestUrl);
                    var review = await sender.Send(request, ct);
                    return Results.Ok(review);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Ai")
                .WithName("ReviewPullRequest");
        }

        public record Body(string PullRequestUrl);
    }

    public record Request(string Slug, int Number, string PullRequestUrl) : IRequest<ReviewDto>;

    public record ReviewDto(string CommentId, string Body);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.PullRequestUrl)
                .NotEmpty().WithErrorCode("ai:pr_review:pull_request_url:required");

            RuleFor(x => x.PullRequestUrl)
                .Matches(@"^https://github\.com/[^/]+/[^/]+/pull/\d+$")
                .WithErrorCode("ai:pr_review:pull_request_url:invalid")
                .When(x => !string.IsNullOrEmpty(x.PullRequestUrl));
        }
    }

    public class RequestHandler(AppDbContext db, IChatClient chatClient, GitHubClient gitHub, IdFactory idFactory)
        : IRequestHandler<Request, ReviewDto>
    {
        public async Task<ReviewDto> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var issue = await db.Issues
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var tools = new[]
            {
                AIFunctionFactory.Create(
                    (CancellationToken toolCt) => gitHub.FetchMetadataAsync(request.PullRequestUrl, toolCt),
                    "fetch_pr_metadata",
                    "Fetch the PR title, author, base/head branches, and description."),
                AIFunctionFactory.Create(
                    (CancellationToken toolCt) => gitHub.FetchDiffAsync(request.PullRequestUrl, toolCt),
                    "fetch_pr_diff",
                    "Fetch the unified diff of all changes in the PR (capped at 50 KB)."),
                AIFunctionFactory.Create(
                    (CancellationToken toolCt) => gitHub.FetchChangedFilesAsync(request.PullRequestUrl, toolCt),
                    "fetch_changed_files",
                    "Fetch the list of files changed in the PR with their status (added/modified/removed)."),
                AIFunctionFactory.Create(
                    (string path, CancellationToken toolCt) => gitHub.FetchFileContentAsync(request.PullRequestUrl, path, toolCt),
                    "fetch_file_content",
                    "Fetch the full content of a specific file from the PR's head ref (capped at 20 KB)."),
            };

            var agent = chatClient.AsBuilder()
                .UseFunctionInvocation(loggerFactory: null, configure: o => o.MaximumIterationsPerRequest = 10)
                .Build();

            var options = new ChatOptions { Tools = [..tools] };

            var systemPrompt = issue.AcceptanceCriteria is { Length: > 0 } criteria
                ? $"""
                  You are a code reviewer. Review this GitHub PR against the following acceptance criteria.
                  For each criterion, explicitly state whether the PR satisfies it and why.
                  Output a concise markdown review.

                  Acceptance criteria:
                  {criteria}
                  """
                : """
                  You are a code reviewer. Review this GitHub PR for general code quality,
                  correctness, and best practices. Output a concise markdown review.
                  """;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, $"Please review this pull request: {request.PullRequestUrl}"),
            };

            string reviewText;
            try
            {
                var response = await agent.GetResponseAsync(messages, options, ct);
                reviewText = response.Text;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new BadGatewayException("LLM service timed out.", "ai:pr_review:llm:unavailable");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new BadGatewayException("LLM service is unavailable.", "ai:pr_review:llm:unavailable");
            }

            var now = DateTimeOffset.UtcNow;
            var trimmed = reviewText.Trim();
            var body = trimmed[..Math.Min(10_000, trimmed.Length)];

            var comment = new Comment
            {
                Id = idFactory.Create(),
                IssueId = issue.Id,
                AuthorId = SystemUsers.AiUserId,
                Body = body,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Comments.Add(comment);
            await db.SaveChangesAsync(ct);

            return new ReviewDto(IdEncoding.Encode(comment.Id), comment.Body);
        }
    }
}
