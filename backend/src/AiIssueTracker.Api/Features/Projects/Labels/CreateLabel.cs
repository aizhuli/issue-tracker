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

namespace AiIssueTracker.Api.Features.Projects.Labels;

public static class CreateLabel
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects/{slug}/labels", async (
                    string slug,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(slug, body.Name, body.Color);
                    var response = await sender.Send(request, ct);
                    return Results.Created(
                        $"/api/projects/{slug}/labels/{response.Id}",
                        response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Labels")
                .WithName("CreateLabel");
        }

        public record Body(string Name, string Color);
    }

    public record Request(string Slug, string Name, string Color) : IRequest<LabelDto>;

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("projects:label:name:required_or_too_long")
                .MaximumLength(40).WithErrorCode("projects:label:name:required_or_too_long");

            RuleFor(x => x.Color)
                .NotEmpty().WithErrorCode("projects:label:color:invalid")
                .Matches(@"^#[0-9A-Fa-f]{6}$").WithErrorCode("projects:label:color:invalid");
        }
    }

    public class RequestHandler(AppDbContext db, IdFactory idFactory)
        : IRequestHandler<Request, LabelDto>
    {
        public async Task<LabelDto> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct);

            if (project is null)
            {
                throw new NotFoundException("Project not found.", "projects:project:not_found");
            }

            var count = await db.Labels.CountAsync(l => l.ProjectId == project.Id, ct);
            if (count >= 200)
            {
                throw new BadRequestException(
                    "Project has reached the maximum of 200 labels.",
                    "projects:label:too_many");
            }

            var now = DateTimeOffset.UtcNow;
            var label = new Label
            {
                Id = idFactory.Create(),
                ProjectId = project.Id,
                Name = request.Name.Trim().ToLowerInvariant(),
                Color = request.Color,
                CreatedAt = now,
            };

            db.Labels.Add(label);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                throw new ConflictException(
                    "A label with that name already exists in this project.",
                    "projects:label:name:already_exists");
            }

            return new LabelDto(
                IdEncoding.Encode(label.Id),
                label.Name,
                label.Color,
                label.CreatedAt);
        }
    }
}
