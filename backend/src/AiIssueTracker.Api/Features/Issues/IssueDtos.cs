using AiIssueTracker.Api.Data.Entities;

namespace AiIssueTracker.Api.Features.Issues;

public record UserSummary(string Id, string Name, string Email, string? AvatarUrl);
public record LabelSummary(string Id, string Name, string Color);

public record IssueFull(
    string Id,
    int Number,
    string DisplayKey,
    string Title,
    string? Description,
    string Status,
    string Priority,
    UserSummary? Assignee,
    UserSummary Reporter,
    LabelSummary[] Labels,
    string? AcceptanceCriteria,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public record IssueSummary(
    string Id,
    int Number,
    string DisplayKey,
    string Title,
    string Status,
    string Priority,
    UserSummary? Assignee,
    LabelSummary[] Labels,
    int CommentCount,
    DateTimeOffset UpdatedAt);

internal static class IssueMapping
{
    private static readonly string[] ValidStatuses = ["backlog", "todo", "in-progress", "in-review", "done"];
    private static readonly string[] ValidPriorities = ["low", "medium", "high", "urgent"];

    internal static bool IsValidStatus(string? s) =>
        s is not null && ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase);

    internal static bool IsValidPriority(string? s) =>
        s is not null && ValidPriorities.Contains(s, StringComparer.OrdinalIgnoreCase);

    internal static string ToKebab(this IssueStatus s) => s switch
    {
        IssueStatus.InProgress => "in-progress",
        IssueStatus.InReview => "in-review",
        _ => s.ToString().ToLowerInvariant()
    };

    internal static string ToKebab(this IssuePriority p) => p.ToString().ToLowerInvariant();

    internal static IssueStatus ParseStatus(string s) => s.ToLowerInvariant() switch
    {
        "backlog" => IssueStatus.Backlog,
        "todo" => IssueStatus.Todo,
        "in-progress" => IssueStatus.InProgress,
        "in-review" => IssueStatus.InReview,
        "done" => IssueStatus.Done,
        _ => throw new ArgumentException($"Unknown status: {s}")
    };

    internal static IssuePriority ParsePriority(string s) => s.ToLowerInvariant() switch
    {
        "low" => IssuePriority.Low,
        "medium" => IssuePriority.Medium,
        "high" => IssuePriority.High,
        "urgent" => IssuePriority.Urgent,
        _ => throw new ArgumentException($"Unknown priority: {s}")
    };
}
