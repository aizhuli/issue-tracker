namespace AiIssueTracker.Api.Data.Entities;

public class IssueLabel
{
    public long IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public long LabelId { get; set; }
    public Label Label { get; set; } = null!;
}
