using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class UpdateIssueTests(TestFixture fixture) : IAsyncLifetime
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

    private static Issue MakeIssue(
        long id,
        long projectId,
        long reporterId,
        int number,
        string title,
        IssueStatus status = IssueStatus.Backlog,
        IssuePriority priority = IssuePriority.Medium,
        long? assigneeId = null,
        DateTimeOffset? createdAt = null)
    {
        var ts = createdAt ?? DateTimeOffset.UtcNow;
        return new Issue
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = title,
            Status = status,
            Priority = priority,
            AssigneeId = assigneeId,
            CreatedAt = ts,
            UpdatedAt = ts,
        };
    }

    private static List<string> ExtractValidationErrorCodes(JsonDocument doc)
    {
        var codes = new List<string>();
        if (!doc.RootElement.TryGetProperty("errors", out var errors)) return codes;
        foreach (var field in errors.EnumerateObject())
            foreach (var entry in field.Value.EnumerateArray())
                if (entry.TryGetProperty("code", out var code))
                    codes.Add(code.GetString()!);
        return codes;
    }

    // -------------------------------------------------------------------------
    // Happy path: updates every field; UpdatedAt bumped past CreatedAt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_HappyPath_UpdatesAllFieldsAndBumpsUpdatedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);

        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(11L, "assignee@example.com", "Assignee");
        var project  = MakeProject(20L, 10L, "upd-proj");
        var label    = new Label { Id = 30L, ProjectId = 20L, Name = "feature", Color = "#0000ff", CreatedAt = DateTimeOffset.UtcNow };
        var issue    = MakeIssue(40L, 20L, 10L, 1, "Old Title", createdAt: past);
        await fixture.Database.Save(reporter, assignee, project, label, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PutAsJsonAsync("/api/projects/upd-proj/issues/1", new
        {
            title              = "New Title",
            description        = "Updated description",
            status             = "todo",
            priority           = "high",
            assigneeId         = IdEncoding.Encode(11L),
            labelIds           = new[] { IdEncoding.Encode(30L) },
            acceptanceCriteria = "AC updated",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Title.Should().Be("New Title");
        body.Description.Should().Be("Updated description");
        body.Status.Should().Be("todo");
        body.Priority.Should().Be("high");
        body.Assignee.Should().NotBeNull();
        body.Assignee!.Id.Should().Be(IdEncoding.Encode(11L));
        body.Labels.Should().HaveCount(1);
        body.Labels[0].Name.Should().Be("feature");
        body.AcceptanceCriteria.Should().Be("AC updated");
        body.UpdatedAt.Should().BeAfter(past);
    }

    // -------------------------------------------------------------------------
    // Transition into Done sets ClosedAt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_TransitionIntoDone_SetsClosedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var project  = MakeProject(20L, 10L, "done-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Issue");
        await fixture.Database.Save(reporter, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PutAsJsonAsync("/api/projects/done-proj/issues/1", new
        {
            title    = "Issue",
            status   = "done",
            priority = "medium",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.ClosedAt.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Transition out of Done clears ClosedAt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_TransitionOutOfDone_ClearsClosedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var project  = MakeProject(20L, 10L, "undone-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1, "Done Issue", status: IssueStatus.Done);
        issue.ClosedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await fixture.Database.Save(reporter, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PutAsJsonAsync("/api/projects/undone-proj/issues/1", new
        {
            title    = "Done Issue",
            status   = "todo",
            priority = "medium",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.ClosedAt.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // labelIds with id from another project → 400; original labels unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_LabelFromDifferentProject_Returns400AndPreservesOriginalLabels()
    {
        var ct = TestContext.Current.CancellationToken;
        var user     = MakeUser(10L, "user@example.com", "User");
        var projectA = MakeProject(20L, 10L, "lbl-a");
        var projectB = MakeProject(21L, 10L, "lbl-b");
        var labelInA = new Label { Id = 30L, ProjectId = 20L, Name = "valid", Color = "#aabbcc", CreatedAt = DateTimeOffset.UtcNow };
        var labelInB = new Label { Id = 31L, ProjectId = 21L, Name = "foreign", Color = "#112233", CreatedAt = DateTimeOffset.UtcNow };
        var issue    = MakeIssue(40L, 20L, 10L, 1, "Labelled Issue");
        var link     = new IssueLabel { IssueId = 40L, LabelId = 30L };
        await fixture.Database.Save(user, projectA, projectB, labelInA, labelInB, issue, link);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PutAsJsonAsync("/api/projects/lbl-a/issues/1", new
        {
            title    = "Labelled Issue",
            status   = "backlog",
            priority = "medium",
            labelIds = new[] { IdEncoding.Encode(31L) },  // belongs to project B
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:labels:not_in_project");

        // Original label link must still exist in the DB
        var remaining = await fixture.Database.SingleOrDefault<IssueLabel>(
            il => il.IssueId == 40L && il.LabelId == 30L, ct);
        remaining.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // assigneeId referencing unknown user → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_UnknownAssigneeId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "asgn-proj");
        var issue   = MakeIssue(30L, 20L, 10L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PutAsJsonAsync("/api/projects/asgn-proj/issues/1", new
        {
            title      = "Issue",
            status     = "backlog",
            priority   = "medium",
            assigneeId = IdEncoding.Encode(99999L),
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:assignee:not_found");
    }

    // -------------------------------------------------------------------------
    // Full-PUT semantics: omitted optional fields are cleared in the response
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIssue_OmittedOptionalFields_ClearsThemFromIssue()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(11L, "assignee@example.com", "Assignee");
        var project  = MakeProject(20L, 10L, "clear-proj");
        var label    = new Label { Id = 30L, ProjectId = 20L, Name = "tag", Color = "#123456", CreatedAt = DateTimeOffset.UtcNow };
        var issue    = MakeIssue(40L, 20L, 10L, 1, "Issue With Data", assigneeId: 11L);
        issue.Description = "Some description";
        issue.AcceptanceCriteria = "Some AC";
        var link = new IssueLabel { IssueId = 40L, LabelId = 30L };
        await fixture.Database.Save(reporter, assignee, project, label, issue, link);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        // PUT with only required fields — description, acceptanceCriteria, assigneeId, labelIds omitted
        var response = await client.PutAsJsonAsync("/api/projects/clear-proj/issues/1", new
        {
            title    = "Issue With Data",
            status   = "backlog",
            priority = "medium",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Description.Should().BeNull();
        body.AcceptanceCriteria.Should().BeNull();
        body.Assignee.Should().BeNull();
        body.Labels.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly UpdateIssue.RequestValidator _validator = new();

        private static UpdateIssue.Request Valid(
            string title = "Valid Title",
            string? description = null,
            string status = "backlog",
            string priority = "medium",
            string? assigneeId = null,
            string[]? labelIds = null,
            string? acceptanceCriteria = null) =>
            new("slug", 1, title, description, status, priority, assigneeId, labelIds, acceptanceCriteria);

        [Fact]
        public void Title_empty_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(title: ""));
            result.ShouldHaveValidationErrorFor(x => x.Title)
                .WithErrorCode("issues:issue:title:required_or_too_long");
        }

        [Fact]
        public void Title_201_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(title: new string('a', 201)));
            result.ShouldHaveValidationErrorFor(x => x.Title)
                .WithErrorCode("issues:issue:title:required_or_too_long");
        }

        [Fact]
        public void Title_200_chars_passes()
        {
            var result = _validator.TestValidate(Valid(title: new string('a', 200)));
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Description_10001_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(description: new string('d', 10001)));
            result.ShouldHaveValidationErrorFor(x => x.Description)
                .WithErrorCode("issues:issue:description:too_long");
        }

        [Fact]
        public void Description_10000_chars_passes()
        {
            var result = _validator.TestValidate(Valid(description: new string('d', 10000)));
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void AcceptanceCriteria_10001_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(acceptanceCriteria: new string('x', 10001)));
            result.ShouldHaveValidationErrorFor(x => x.AcceptanceCriteria)
                .WithErrorCode("issues:issue:acceptance_criteria:too_long");
        }

        [Fact]
        public void AcceptanceCriteria_10000_chars_passes()
        {
            var result = _validator.TestValidate(Valid(acceptanceCriteria: new string('x', 10000)));
            result.ShouldNotHaveValidationErrorFor(x => x.AcceptanceCriteria);
        }

        [Fact]
        public void Status_invalid_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(status: "nope"));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_all_valid_values_pass()
        {
            foreach (var status in new[] { "backlog", "todo", "in-progress", "in-review", "done" })
            {
                var result = _validator.TestValidate(Valid(status: status));
                result.ShouldNotHaveValidationErrorFor(x => x.Status);
            }
        }

        [Fact]
        public void Priority_invalid_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(Valid(priority: "nope"));
            result.ShouldHaveValidationErrorFor(x => x.Priority)
                .WithErrorCode("issues:issue:priority:invalid");
        }

        [Fact]
        public void LabelIds_21_entries_fails_with_correct_error_code()
        {
            var ids = Enumerable.Repeat("someid", 21).ToArray();
            var result = _validator.TestValidate(Valid(labelIds: ids));
            result.ShouldHaveValidationErrorFor(x => x.LabelIds)
                .WithErrorCode("issues:issue:labels:too_many");
        }
    }
}
