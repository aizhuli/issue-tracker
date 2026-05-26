namespace AiIssueTracker.Api.Data.Entities;

public class Issue
{
    public long Id { get; set; }                       // IdGen
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int Number { get; set; }                    // per-project sequential
    public string Title { get; set; } = "";            // 1–200
    public string? Description { get; set; }           // 0–10_000, markdown
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public long? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public long ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    public string? AcceptanceCriteria { get; set; }    // 0–10_000, markdown
    public bool AcceptanceCriteriaAiSuggested { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<IssueLabel> IssueLabels { get; set; } = [];
}

public enum IssueStatus { Backlog, Todo, InProgress, InReview, Done }
public enum IssuePriority { Low, Medium, High, Urgent }
