using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class ChangeStatusTests(TestFixture fixture) : IAsyncLifetime
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
        IssueStatus status = IssueStatus.Backlog) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = title,
            Status = status,
            Priority = IssuePriority.Medium,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // 200 for each valid status; response status matches what was sent
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("backlog")]
    [InlineData("todo")]
    [InlineData("in-progress")]
    [InlineData("in-review")]
    [InlineData("done")]
    public async Task ChangeStatus_EachValidStatus_Returns200WithMatchingStatus(string newStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "cs-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/cs-proj/issues/1/status", new { status = newStatus }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.Status.Should().Be(newStatus);
    }

    // -------------------------------------------------------------------------
    // Transitioning into Done sets ClosedAt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeStatus_TransitionToDone_SetsClosedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "done-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Issue", status: IssueStatus.InProgress);
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/done-proj/issues/1/status", new { status = "done" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.ClosedAt.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Transitioning out of Done clears ClosedAt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeStatus_TransitionOutOfDone_ClearsClosedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "undone-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Done Issue", status: IssueStatus.Done);
        issue.ClosedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/undone-proj/issues/1/status", new { status = "in-review" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.ClosedAt.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Unknown issue number → 404 issues:issue:not_found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeStatus_UnknownNumber_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "miss-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/miss-proj/issues/999/status", new { status = "todo" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:not_found");
    }

    // -------------------------------------------------------------------------
    // Invalid enum value → 400 issues:issue:status:invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeStatus_InvalidStatusValue_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "inv-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/inv-proj/issues/1/status", new { status = "foo" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("common:validation:failed");
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly ChangeStatus.RequestValidator _validator = new();

        [Fact]
        public void Status_invalid_value_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(new ChangeStatus.Request("slug", 1, "foo"));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_null_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(new ChangeStatus.Request("slug", 1, null!));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_all_valid_values_pass()
        {
            foreach (var status in new[] { "backlog", "todo", "in-progress", "in-review", "done" })
            {
                var result = _validator.TestValidate(new ChangeStatus.Request("slug", 1, status));
                result.ShouldNotHaveValidationErrorFor(x => x.Status);
            }
        }
    }
}
