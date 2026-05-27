using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects.Labels;

public record LabelDto(string Id, string Name, string Color, DateTimeOffset CreatedAt);

public static class ListLabels
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/{slug}/labels", async (
                    string slug,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Labels")
                .WithName("ListLabels");
        }
    }

    public record Request(string Slug) : IRequest<LabelDto[]>;

    public class RequestHandler(AppDbContext db) : IRequestHandler<Request, LabelDto[]>
    {
        public async Task<LabelDto[]> Handle(Request request, CancellationToken ct)
        {
            var projectExists = await db.Projects
                .AnyAsync(p => p.Slug == request.Slug, ct);

            if (!projectExists)
            {
                throw new NotFoundException("Project not found.", "projects:project:not_found");
            }

            return await db.Labels
                .Where(l => l.Project.Slug == request.Slug)
                .OrderBy(l => l.Name)
                .Select(l => new LabelDto(
                    IdEncoding.Encode(l.Id),
                    l.Name,
                    l.Color,
                    l.CreatedAt))
                .Take(200)
                .ToArrayAsync(ct);
        }
    }
}
