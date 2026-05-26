using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Issues;

public static class DeleteIssue
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/projects/{slug}/issues/{number}", async (
                    string slug, int number, ISender sender, CancellationToken ct) =>
                {
                    await sender.Send(new Request(slug, number), ct);
                    return Results.NoContent();
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Issues").WithName("DeleteIssue");
        }
    }

    public record Request(string Slug, int Number) : IRequest;

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request>
    {
        public async Task Handle(Request request, CancellationToken ct)
        {
            var issue = await db.Issues
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Project.Slug == request.Slug && i.Number == request.Number, ct);

            if (issue is null)
            {
                throw new NotFoundException("Issue not found.", "issues:issue:not_found");
            }

            if (currentUser.UserId != issue.ReporterId && currentUser.UserId != issue.Project.OwnerId)
            {
                throw new ForbiddenDomainException("Not allowed.", "issues:issue:delete:forbidden");
            }

            db.Issues.Remove(issue);
            await db.SaveChangesAsync(ct);
        }
    }
}
