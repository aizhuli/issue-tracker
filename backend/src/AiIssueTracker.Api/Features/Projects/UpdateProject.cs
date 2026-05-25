using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects;

public static class UpdateProject
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("/api/projects/{slug}", async (
                    string slug,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug, body.Name, body.Description), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("UpdateProject");
        }

        public record Body(string Name, string? Description);
    }

    public record Request(string Slug, string Name, string? Description) : IRequest<Response>;

    public record Response(
        string Id,
        string Slug,
        string Name,
        string? Description,
        string OwnerId,
        string OwnerName,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("projects:project:name:required_or_too_long")
                .MaximumLength(100).WithErrorCode("projects:project:name:required_or_too_long");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithErrorCode("projects:project:description:too_long")
                .When(x => x.Description is not null);
        }
    }

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
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

            if (project.OwnerId != currentUser.UserId)
            {
                throw new ForbiddenDomainException(
                    "Only the owner can edit this project.",
                    "projects:project:edit:forbidden");
            }

            project.Name = request.Name.Trim();
            project.Description = request.Description?.Trim();
            project.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

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
