using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class ChangeAssigneeTests(TestFixture fixture) : IAsyncLifetime
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
        long? assigneeId = null, DateTimeOffset? updatedAt = null) =>
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
            CreatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // 200 with valid user: assignee populated; UpdatedAt bumped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeAssignee_WithValidUser_Returns200WithAssigneeAndBumpsUpdatedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);

        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(11L, "assignee@example.com", "Assignee");
        var project  = MakeProject(20L, 10L, "asgn-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Issue", updatedAt: past);
        await fixture.Database.Save(reporter, assignee, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/asgn-proj/issues/1/assignee",
            new { assigneeId = IdEncoding.Encode(11L) }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Assignee.Should().NotBeNull();
        body.Assignee!.Id.Should().Be(IdEncoding.Encode(11L));
        body.Assignee.Name.Should().Be("Assignee");
        body.UpdatedAt.Should().BeAfter(past);
    }

    // -------------------------------------------------------------------------
    // 200 with null: clears assignment; UpdatedAt bumped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeAssignee_WithNull_Returns200WithNullAssigneeAndBumpsUpdatedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);

        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(11L, "assignee@example.com", "Assignee");
        var project  = MakeProject(20L, 10L, "clear-asgn-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Issue", assigneeId: 11L, updatedAt: past);
        await fixture.Database.Save(reporter, assignee, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/clear-asgn-proj/issues/1/assignee",
            new { assigneeId = (string?)null }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Assignee.Should().BeNull();
        body.UpdatedAt.Should().BeAfter(past);
    }

    // -------------------------------------------------------------------------
    // 404 with unknown user id
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChangeAssignee_WithUnknownUserId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "miss-asgn-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PatchAsJsonAsync(
            "/api/projects/miss-asgn-proj/issues/1/assignee",
            new { assigneeId = IdEncoding.Encode(99999L) }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:assignee:not_found");
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly ChangeAssignee.RequestValidator _validator = new();

        [Fact]
        public void AssigneeId_null_passes()
        {
            var result = _validator.TestValidate(new ChangeAssignee.Request("slug", 1, null));
            result.ShouldNotHaveValidationErrorFor(x => x.AssigneeId);
        }

        [Fact]
        public void AssigneeId_valid_base32_passes()
        {
            var result = _validator.TestValidate(
                new ChangeAssignee.Request("slug", 1, IdEncoding.Encode(42L)));
            result.ShouldNotHaveValidationErrorFor(x => x.AssigneeId);
        }

        [Fact]
        public void AssigneeId_malformed_base32_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new ChangeAssignee.Request("slug", 1, "!!!not-valid!!!"));
            result.ShouldHaveValidationErrorFor(x => x.AssigneeId)
                .WithErrorCode("issues:issue:assignee:not_found");
        }
    }
}
