using System.Text.RegularExpressions;
using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Features.Projects;

public static class SlugAvailability
{
    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9](?:[a-z0-9-]{1,48}[a-z0-9])?$",
        RegexOptions.Compiled);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/projects/slug-availability", async (
                    [FromQuery] string? slug,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var response = await sender.Send(new Request(slug ?? ""), ct);
                    return Results.Ok(response);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Projects")
                .WithName("SlugAvailability");
        }
    }

    public record Request(string Slug) : IRequest<Response>;

    public record Response(string Slug, bool Available, string? Reason);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Slug).NotEmpty().WithErrorCode("projects:project:slug:missing");
        }
    }

    public class RequestHandler(AppDbContext db) : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            if (!SlugRegex.IsMatch(request.Slug))
                return new Response(request.Slug, false, "invalid_format");

            var taken = await db.Projects.AnyAsync(p => p.Slug == request.Slug, ct);
            return taken
                ? new Response(request.Slug, false, "taken")
                : new Response(request.Slug, true, null);
        }
    }
}
