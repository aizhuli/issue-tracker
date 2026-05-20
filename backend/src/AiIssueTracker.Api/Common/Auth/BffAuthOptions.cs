namespace AiIssueTracker.Api.Common.Auth;

public class BffAuthOptions
{
    public const string SectionName = "BffAuth";

    public string SharedSecret { get; set; } = string.Empty;
}
