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

namespace AiIssueTracker.Api.Features.Comments;

public static class CreateComment
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects/{slug}/issues/{number}/comments", async (
                    string slug,
                    int number,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug, number, body.Content), ct);
                    return Results.Created(
                        $"/api/projects/{slug}/issues/{number}/comments/{response.Id}",
                        response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Comments")
                .WithName("CreateComment");
        }

        public record Body([property: System.Text.Json.Serialization.JsonPropertyName("body")] string Content);
    }

    public record Request(string Slug, int Number, string Body) : IRequest<CommentDto>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Body)
                .NotEmpty().WithErrorCode("comments:comment:body:required_or_too_long")
                .MaximumLength(10000).WithErrorCode("comments:comment:body:required_or_too_long");
        }
    }

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser, IdFactory idFactory)
        : IRequestHandler<Request, CommentDto>
    {
        public async Task<CommentDto> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var issue = await db.Issues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var now = DateTimeOffset.UtcNow;

            var comment = new Comment
            {
                Id = idFactory.Create(),
                IssueId = issue.Id,
                AuthorId = currentUser.UserId,
                Body = request.Body.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Comments.Add(comment);
            await db.SaveChangesAsync(ct);

            var author = await db.Users
                .AsNoTracking()
                .FirstAsync(u => u.Id == comment.AuthorId, ct);

            return new CommentDto(
                IdEncoding.Encode(comment.Id),
                IdEncoding.Encode(comment.AuthorId),
                author.Name,
                author.Avatar,
                comment.Body,
                comment.CreatedAt,
                comment.UpdatedAt,
                false);
        }
    }
}
