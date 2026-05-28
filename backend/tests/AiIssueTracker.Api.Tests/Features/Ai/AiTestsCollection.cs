namespace AiIssueTracker.Api.Tests.Features.Ai;

[CollectionDefinition(Name)]
public class AiTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(AiTestsCollection);
}
