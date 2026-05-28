namespace AiIssueTracker.Api.Integrations.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string? BaseUrl { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }
    // Consumed at the call site to bound the CancellationToken per LLM request.
    public int TimeoutSeconds { get; init; } = 30;
}
