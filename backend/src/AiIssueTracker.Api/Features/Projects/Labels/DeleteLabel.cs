using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects.Labels;

public static class DeleteLabel
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("/api/projects/{slug}/labels/{labelId}", async (
                    string slug,
                    string labelId,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    await sender.Send(new Request(slug, labelId), ct);
                    return Results.NoContent();
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Labels")
                .WithName("DeleteLabel");
        }
    }

    public record Request(string Slug, string LabelId) : IRequest<Unit>;

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
        : IRequestHandler<Request, Unit>
    {
        public async Task<Unit> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct);

            if (project is null)
            {
                throw new NotFoundException("Project not found.", "projects:project:not_found");
            }

            if (project.OwnerId != currentUser.UserId)
            {
                throw new ForbiddenDomainException(
                    "Only the project owner can delete labels.",
                    "projects:label:edit:forbidden");
            }

            if (!IdEncoding.TryDecode(request.LabelId, out var labelId))
            {
                throw new NotFoundException("Label not found.", "projects:label:not_found");
            }

            var label = await db.Labels
                .FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == project.Id, ct);

            if (label is null)
            {
                throw new NotFoundException("Label not found.", "projects:label:not_found");
            }

            db.Labels.Remove(label);
            await db.SaveChangesAsync(ct);

            return Unit.Value;
        }
    }
}
