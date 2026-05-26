namespace AiIssueTracker.Api.Data.Entities;

public class Label
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = "";             // 1–40, stored lowercase
    public string Color { get; set; } = "";            // hex, e.g. "#6B7280"
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<IssueLabel> IssueLabels { get; set; } = [];
}
