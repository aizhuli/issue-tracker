using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Comments;

public static class DeleteComment
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/projects/{slug}/issues/{number}/comments/{commentId}", async (
                    string slug,
                    int number,
                    string commentId,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    await sender.Send(new Request(slug, number, commentId), ct);
                    return Results.NoContent();
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Comments")
                .WithName("DeleteComment");
        }
    }

    public record Request(string Slug, int Number, string CommentId) : IRequest;

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request>
    {
        public async Task Handle(Request request, CancellationToken ct)
        {
            if (!IdEncoding.TryDecode(request.CommentId, out var commentId))
                throw new NotFoundException("Comment not found.", "comments:comment:not_found");

            var comment = await db.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId, ct)
                ?? throw new NotFoundException("Comment not found.", "comments:comment:not_found");

            if (comment.AuthorId != currentUser.UserId)
                throw new ForbiddenDomainException(
                    "Only the author can delete this comment.",
                    "comments:comment:delete:forbidden");

            db.Comments.Remove(comment);
            await db.SaveChangesAsync(ct);
        }
    }
}
