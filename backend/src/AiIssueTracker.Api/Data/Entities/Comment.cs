namespace AiIssueTracker.Api.Data.Entities;

public class Comment
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public long AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public string Body { get; set; } = "";             // 1–10_000, markdown
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
