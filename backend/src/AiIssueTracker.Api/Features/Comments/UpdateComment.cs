using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Comments;

public static class UpdateComment
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("/api/projects/{slug}/issues/{number}/comments/{commentId}", async (
                    string slug,
                    int number,
                    string commentId,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug, number, commentId, body.Content), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Comments")
                .WithName("UpdateComment");
        }

        public record Body([property: System.Text.Json.Serialization.JsonPropertyName("body")] string Content);
    }

    public record Request(string Slug, int Number, string CommentId, string Body) : IRequest<CommentDto>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Body)
                .NotEmpty().WithErrorCode("comments:comment:body:required_or_too_long")
                .MaximumLength(10000).WithErrorCode("comments:comment:body:required_or_too_long");
        }
    }

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request, CommentDto>
    {
        public async Task<CommentDto> Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.CommentId, out var commentId))
                throw new NotFoundException("Comment not found.", "comments:comment:not_found");

            var comment = await db.Comments
                .Include(c => c.Author)
                .FirstOrDefaultAsync(c => c.Id == commentId, ct)
                ?? throw new NotFoundException("Comment not found.", "comments:comment:not_found");

            if (comment.AuthorId != currentUser.UserId)
                throw new ForbiddenDomainException(
                    "Only the author can edit this comment.",
                    "comments:comment:update:forbidden");

            var now = DateTimeOffset.UtcNow;
            comment.Body = request.Body.Trim();
            comment.UpdatedAt = now;

            await db.SaveChangesAsync(ct);

            return new CommentDto(
                IdEncoding.Encode(comment.Id),
                IdEncoding.Encode(comment.AuthorId),
                comment.Author.Name,
                comment.Author.Avatar,
                comment.Body,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.UpdatedAt > comment.CreatedAt.AddSeconds(1));
        }
    }
}
