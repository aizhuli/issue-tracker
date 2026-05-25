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
using Npgsql;

namespace AiIssueTracker.Api.Features.Projects;

public static class CreateProject
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects", async (
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(body.Name, body.Slug, body.Description);
                    var response = await sender.Send(request, ct);
                    return Results.Created($"/api/projects/{response.Slug}", response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("CreateProject");
        }

        public record Body(string Name, string Slug, string? Description);
    }

    public record Request(string Name, string Slug, string? Description) : IRequest<Response>;

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

            RuleFor(x => x.Slug)
                .NotEmpty().WithErrorCode("projects:project:slug:invalid_format")
                .Matches(@"^[a-z0-9](?:[a-z0-9-]{1,48}[a-z0-9])?$")
                    .WithErrorCode("projects:project:slug:invalid_format");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithErrorCode("projects:project:description:too_long")
                .When(x => x.Description is not null);
        }
    }

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser, IdFactory idFactory)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;

            var project = new Project
            {
                Id = idFactory.Create(),
                Slug = request.Slug.ToLowerInvariant(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                OwnerId = currentUser.UserId,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Projects.Add(project);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                throw new ConflictException(
                    "Slug already taken.",
                    "projects:project:slug:already_exists");
            }

            var loaded = await db.Projects
                .Include(p => p.Owner)
                .FirstAsync(p => p.Id == project.Id, ct);

            return new Response(
                Id: IdEncoding.Encode(loaded.Id),
                Slug: loaded.Slug,
                Name: loaded.Name,
                Description: loaded.Description,
                OwnerId: IdEncoding.Encode(loaded.OwnerId),
                OwnerName: loaded.Owner.Name,
                CreatedAt: loaded.CreatedAt,
                UpdatedAt: loaded.UpdatedAt);
        }
    }
}
