using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiIssueTracker.Api.Features.Projects.Labels;

public static class UpdateLabel
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("/api/projects/{slug}/labels/{labelId}", async (
                    string slug,
                    string labelId,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(slug, labelId, body.Name, body.Color);
                    var response = await sender.Send(request, ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Labels")
                .WithName("UpdateLabel");
        }

        public record Body(string Name, string Color);
    }

    public record Request(string Slug, string LabelId, string Name, string Color) : IRequest<LabelDto>;

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

    public class RequestHandler(AppDbContext db, ICurrentUser currentUser)
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

            if (project.OwnerId != currentUser.UserId)
            {
                throw new ForbiddenDomainException(
                    "Only the project owner can edit labels.",
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

            label.Name = request.Name.Trim().ToLowerInvariant();
            label.Color = request.Color;

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
