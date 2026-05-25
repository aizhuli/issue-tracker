using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects;

public static class GetProject
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/{slug}", async (
                    string slug,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("GetProject");
        }
    }

    public record Request(string Slug) : IRequest<Response>;

    public record Response(
        string Id,
        string Slug,
        string Name,
        string? Description,
        string OwnerId,
        string OwnerName,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public class RequestHandler(AppDbContext db)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .Include(p => p.Owner)
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct);

            if (project is null)
            {
                throw new NotFoundException(
                    "Project not found.",
                    "projects:project:not_found");
            }

            return new Response(
                Id: IdEncoding.Encode(project.Id),
                Slug: project.Slug,
                Name: project.Name,
                Description: project.Description,
                OwnerId: IdEncoding.Encode(project.OwnerId),
                OwnerName: project.Owner.Name,
                CreatedAt: project.CreatedAt,
                UpdatedAt: project.UpdatedAt);
        }
    }
}
