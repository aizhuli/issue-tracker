using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class GetIssueTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email, string name) =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Project MakeProject(long id, long ownerId, string slug) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = "Project",
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Issue MakeIssue(long id, long projectId, long reporterId, int number, string title,
        long? assigneeId = null) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = title,
            Status = IssueStatus.Backlog,
            Priority = IssuePriority.Medium,
            AssigneeId = assigneeId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Comment MakeComment(long id, long issueId, long authorId) =>
        new()
        {
            Id = id,
            IssueId = issueId,
            AuthorId = authorId,
            Body = "A comment",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // 200: full DTO including reporter, assignee, labels, commentCount
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetIssue_BySlugAndNumber_Returns200WithFullDto()
    {
        var ct = TestContext.Current.CancellationToken;

        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(11L, "assignee@example.com", "Assignee");
        var project  = MakeProject(20L, 10L, "full-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Full Issue", assigneeId: 11L);
        var label    = new Label { Id = 40L, ProjectId = 20L, Name = "bug", Color = "#ff0000", CreatedAt = DateTimeOffset.UtcNow };
        var link     = new IssueLabel { IssueId = 30L, LabelId = 40L };
        var comment1 = MakeComment(50L, 30L, 10L);
        var comment2 = MakeComment(51L, 30L, 11L);

        await fixture.Database.Save(reporter, assignee, project, issue, label, link, comment1, comment2);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/projects/full-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Number.Should().Be(1);
        body.DisplayKey.Should().Be("full-proj_1");
        body.Title.Should().Be("Full Issue");
        body.Status.Should().Be("backlog");
        body.Priority.Should().Be("medium");
        body.Reporter.Id.Should().Be(IdEncoding.Encode(10L));
        body.Reporter.Name.Should().Be("Reporter");
        body.Assignee.Should().NotBeNull();
        body.Assignee!.Id.Should().Be(IdEncoding.Encode(11L));
        body.Assignee.Name.Should().Be("Assignee");
        body.Labels.Should().HaveCount(1);
        body.Labels[0].Name.Should().Be("bug");
        body.Labels[0].Color.Should().Be("#ff0000");
        body.CommentCount.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // 404: unknown issue number in the project
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetIssue_UnknownNumber_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "miss-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/projects/miss-proj/issues/999", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:not_found");
    }

    // -------------------------------------------------------------------------
    // 404: number exists in a different project — scope is per-project
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetIssue_NumberExistsInOtherProject_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user     = MakeUser(10L, "user@example.com", "User");
        var projectA = MakeProject(20L, 10L, "proj-a");
        var projectB = MakeProject(21L, 10L, "proj-b");
        var issueInA = MakeIssue(30L, 20L, 10L, 1, "Issue in A");
        await fixture.Database.Save(user, projectA, projectB, issueInA);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/projects/proj-b/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:not_found");
    }

    // -------------------------------------------------------------------------
    // 200: non-owner reader can still read (read is open to any authenticated user)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetIssue_NonOwnerReader_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;

        var owner    = MakeUser(10L, "owner@example.com", "Owner");
        var nonOwner = MakeUser(11L, "reader@example.com", "Reader");
        var project  = MakeProject(20L, 10L, "open-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Open Issue");
        await fixture.Database.Save(owner, nonOwner, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.GetAsync("/api/projects/open-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Number.Should().Be(1);
    }
}
