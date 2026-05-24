namespace AiIssueTracker.Api.Data.Entities;

public class Project
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Issue> Issues { get; set; } = [];
    public ICollection<Label> Labels { get; set; } = [];
}
