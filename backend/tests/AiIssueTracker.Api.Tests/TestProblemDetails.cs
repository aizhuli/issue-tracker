using System.Text.Json.Serialization;

namespace AiIssueTracker.Api.Tests;

public record TestProblemDetails
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("status")] public int? Status { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
    [JsonPropertyName("errorCode")] public string? ErrorCode { get; init; }
    [JsonPropertyName("traceId")] public string? TraceId { get; init; }
}
