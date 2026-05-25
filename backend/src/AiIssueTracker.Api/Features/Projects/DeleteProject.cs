using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects;

public static class DeleteProject
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/projects/{slug}", async (
                    string slug,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    await sender.Send(new Request(slug), ct);
                    return Results.NoContent();
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("DeleteProject");
        }
    }

    public record Request(string Slug) : IRequest<Unit>;

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request, Unit>
    {
        public async Task<Unit> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct);

            if (project is null)
            {
                throw new NotFoundException(
                    "Project not found.",
                    "projects:project:not_found");
            }

            if (project.OwnerId != currentUser.UserId)
            {
                throw new ForbiddenDomainException(
                    "Only the owner can delete this project.",
                    "projects:project:delete:forbidden");
            }

            db.Projects.Remove(project);
            await db.SaveChangesAsync(ct);

            return Unit.Value;
        }
    }
}
