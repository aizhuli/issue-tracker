using System.Text.Json;
using System.Text.Json.Serialization;
using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Features.Issues;
using AiIssueTracker.Api.Integrations.Llm;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AiIssueTracker.Api.Features.Ai;

public static class TriageIssue
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/projects/{slug}/issues/{number}/ai/triage", async (
                    string slug,
                    int number,
                    [FromBody] Body body,
                    ISender sender,
                    CancellationToken ct) =>
                {
                    var request = new Request(slug, number, body.Title, body.Description);
                    var suggestion = await sender.Send(request, ct);
                    return Results.Ok(suggestion);
                })
                .RequireAuthorization(AuthPolicies.RequireUser)
                .WithTags("Ai")
                .WithName("TriageIssue");
        }

        public record Body(string Title, string? Description);
    }

    public record Request(string Slug, int Number, string Title, string? Description)
        : IRequest<TriageSuggestion>;

    public record TriageSuggestion(string Priority, LabelSummary[] Labels, string AcceptanceCriteria);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithErrorCode("ai:triage:title:required_or_too_long")
                .MaximumLength(200).WithErrorCode("ai:triage:title:required_or_too_long");

            RuleFor(x => x.Description)
                .MaximumLength(10000).WithErrorCode("ai:triage:description:too_long")
                .When(x => x.Description is not null);
        }
    }

    public class RequestHandler(AppDbContext db, IChatClient chatClient, IOptions<LlmOptions> llmOptions)
        : IRequestHandler<Request, TriageSuggestion>
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<TriageSuggestion> Handle(Request request, CancellationToken ct)
        {
            var project = await db.Projects
                .Include(p => p.Labels)
                .FirstOrDefaultAsync(p => p.Slug == request.Slug, ct)
                ?? throw new NotFoundException("Project not found.", "projects:project:not_found");

            var issue = await db.Issues
                .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == request.Number, ct)
                ?? throw new NotFoundException("Issue not found.", "issues:issue:not_found");

            var projectLabels = project.Labels.ToList();
            var labelNames = projectLabels.Select(l => l.Name).ToArray();

            var messages = BuildMessages(project.Name, project.Description, labelNames, request.Title, request.Description);
            var options = new ChatOptions
            {
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.Json,
            };

            LlmResponse? parsed = null;

            // First attempt
            parsed = await CallLlmAsync(messages, options, llmOptions.Value.TimeoutSeconds, ct);

            // Retry once on parse failure
            if (parsed is null)
            {
                parsed = await CallLlmAsync(messages, options, llmOptions.Value.TimeoutSeconds, ct);
                if (parsed is null)
                    throw new BadGatewayException("LLM returned invalid response.", "ai:triage:llm:invalid_response");
            }

            // Resolve priority defensively
            var priority = IssueMapping.IsValidPriority(parsed.Priority)
                ? parsed.Priority!.ToLowerInvariant()
                : issue.Priority.ToKebab();

            // Resolve labels: case-insensitive match, drop misses
            var matchedLabels = (parsed.Labels ?? [])
                .Select(name => projectLabels
                    .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Where(l => l is not null)
                .Select(l => new LabelSummary(
                    Id: Common.Identity.IdEncoding.Encode(l!.Id),
                    Name: l.Name,
                    Color: l.Color))
                .ToArray();

            // Resolve acceptance criteria: trim + cap at 10_000 chars
            var acceptanceCriteria = (parsed.AcceptanceCriteria ?? "").Trim();
            if (acceptanceCriteria.Length > 10_000)
                acceptanceCriteria = acceptanceCriteria[..10_000];

            return new TriageSuggestion(priority, matchedLabels, acceptanceCriteria);
        }

        private async Task<LlmResponse?> CallLlmAsync(
            List<ChatMessage> messages,
            ChatOptions options,
            int timeoutSeconds,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            string text;
            try
            {
                var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
                text = response.Text;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new BadGatewayException("LLM service timed out.", "ai:triage:llm:unavailable");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new BadGatewayException("LLM service is unavailable.", "ai:triage:llm:unavailable");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<LlmResponse>(text, JsonOptions);
                if (parsed?.Priority is null && parsed?.AcceptanceCriteria is null)
                    return null;
                return parsed;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static List<ChatMessage> BuildMessages(
            string projectName,
            string? projectDescription,
            string[] labelNames,
            string issueTitle,
            string? issueDescription)
        {
            var labelsSection = labelNames.Length > 0
                ? string.Join(", ", labelNames)
                : "(none)";

            var systemPrompt = """
                You are an expert issue triage assistant. Your job is to analyse a software issue and suggest:
                1. A priority level: one of "low", "medium", "high", "urgent".
                2. Relevant labels from the project's label set only. Never invent labels.
                   If none fit, return an empty array.
                3. Clear, testable acceptance criteria in markdown.

                Respond with ONLY a JSON object — no commentary, no markdown fences:
                {
                  "priority": "<low|medium|high|urgent>",
                  "labels": ["<label name>", ...],
                  "acceptanceCriteria": "<markdown string>"
                }
                """;

            var projectDescPart = string.IsNullOrWhiteSpace(projectDescription)
                ? ""
                : $"\nProject description: {projectDescription}";

            var issueDescPart = string.IsNullOrWhiteSpace(issueDescription)
                ? ""
                : $"\nIssue description:\n{issueDescription}";

            var userMessage = $"""
                Project: {projectName}{projectDescPart}
                Available labels: {labelsSection}

                Issue title: {issueTitle}{issueDescPart}

                Triage this issue.
                """;

            return
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage),
            ];
        }

        private record LlmResponse(
            [property: JsonPropertyName("priority")] string? Priority,
            [property: JsonPropertyName("labels")] string[]? Labels,
            [property: JsonPropertyName("acceptanceCriteria")] string? AcceptanceCriteria);
    }
}
