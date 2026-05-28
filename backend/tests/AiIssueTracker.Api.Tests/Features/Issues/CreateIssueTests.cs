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
public class CreateIssueTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email = "user@example.com", string name = "User") =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Project MakeProject(long id, long ownerId, string slug = "proj") =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = "Project",
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

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
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_WithTitleOnly_Returns201WithCorrectBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(10L, "reporter@example.com", "Reporter");
        var project = MakeProject(20L, 10L, "alpha");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/alpha/issues",
            new { title = "First" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body.Should().NotBeNull();
        body!.Number.Should().Be(1);
        body.Title.Should().Be("First");
        body.Status.Should().Be("backlog");
        body.Priority.Should().Be("medium");
        body.Reporter.Id.Should().Be(IdEncoding.Encode(10L));
        body.DisplayKey.Should().Be("alpha_1");
        body.Assignee.Should().BeNull();
        body.Labels.Should().BeEmpty();
        body.ClosedAt.Should().BeNull();

        response.Headers.Location!.ToString().Should().Be("/api/projects/alpha/issues/1");
    }

    [Fact]
    public async Task CreateIssue_SecondCall_AssignsSequentialNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(11L, "seq@example.com", "Seq User");
        var project = MakeProject(21L, 11L, "beta");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));

        var resp1 = await client.PostAsJsonAsync("/api/projects/beta/issues", new { title = "Issue One" }, ct);
        var resp2 = await client.PostAsJsonAsync("/api/projects/beta/issues", new { title = "Issue Two" }, ct);

        resp1.StatusCode.Should().Be(HttpStatusCode.Created);
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);

        var body1 = await resp1.Content.ReadFromJsonAsync<IssueFull>(ct);
        var body2 = await resp2.Content.ReadFromJsonAsync<IssueFull>(ct);

        body1!.Number.Should().Be(1);
        body2!.Number.Should().Be(2);
    }

    [Fact]
    public async Task CreateIssue_WithTitleOnly_PersistsCorrectDbRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(12L, "db@example.com", "DB User");
        var project = MakeProject(22L, 12L, "gamma");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(12L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/gamma/issues",
            new { title = "DB Check" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);

        IdEncoding.TryDecode(body!.Id, out var issueIdLong).Should().BeTrue();
        var issue = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == issueIdLong, ct);

        issue.Should().NotBeNull();
        issue!.Status.Should().Be(IssueStatus.Backlog);
        issue.Priority.Should().Be(IssuePriority.Medium);
        issue.ReporterId.Should().Be(12L);
    }

    // -------------------------------------------------------------------------
    // Status field in body is silently ignored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_WithStatusInBody_StatusIsIgnoredAndAlwaysBacklog()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(13L, "ignore@example.com", "Ignore User");
        var project = MakeProject(23L, 13L, "delta");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(13L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/delta/issues",
            new { title = "T", status = "done" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
        body!.Status.Should().Be("backlog");
    }

    // -------------------------------------------------------------------------
    // Label from a different project → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_WithLabelFromDifferentProject_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(14L, "label@example.com", "Label User");
        var projectA = MakeProject(24L, 14L, "proj-a");
        var projectB = MakeProject(25L, 14L, "proj-b");
        var labelInB = new Label
        {
            Id = 300L,
            ProjectId = 25L,
            Name = "bug",
            Color = "#ff0000",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.Database.Save(user, projectA, projectB, labelInB);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(14L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/proj-a/issues",
            new { title = "Cross Label", labelIds = new[] { IdEncoding.Encode(300L) } },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:labels:not_in_project");
    }

    // -------------------------------------------------------------------------
    // Non-existent assigneeId → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_WithNonExistentAssigneeId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(15L, "assignee@example.com", "Assignee User");
        var project = MakeProject(26L, 15L, "epsilon");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(15L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/epsilon/issues",
            new { title = "Assign Test", assigneeId = IdEncoding.Encode(99999L) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:assignee:not_found");
    }

    // -------------------------------------------------------------------------
    // Concurrent creates produce contiguous numbers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_ConcurrentCreates_ProduceContiguousNumbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(16L, "concurrent@example.com", "Concurrent User");
        var project = MakeProject(27L, 16L, "zeta");
        await fixture.Database.Save(user, project);

        var encodedUserId = IdEncoding.Encode(16L);

        // Fire 10 parallel POSTs
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
        {
            using var client = fixture.HttpClient.CreateUserClient(encodedUserId);
            var response = await client.PostAsJsonAsync(
                "/api/projects/zeta/issues",
                new { title = $"Concurrent Issue {i}" },
                ct);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await response.Content.ReadFromJsonAsync<IssueFull>(ct);
            return body!.Number;
        }, ct)).ToList();

        var numbers = await Task.WhenAll(tasks);
        var sorted = numbers.OrderBy(n => n).ToArray();
        sorted.Should().Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    }

    // -------------------------------------------------------------------------
    // Validation boundaries (HTTP-level)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIssue_WithEmptyTitle_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(17L, "val1@example.com", "Val User 1");
        var project = MakeProject(28L, 17L, "eta");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(17L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/eta/issues",
            new { title = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("issues:issue:title:required_or_too_long");
    }

    [Fact]
    public async Task CreateIssue_WithTitle200Chars_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(18L, "val2@example.com", "Val User 2");
        var project = MakeProject(29L, 18L, "theta");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(18L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/theta/issues",
            new { title = new string('a', 200) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateIssue_WithTitle201Chars_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(19L, "val3@example.com", "Val User 3");
        var project = MakeProject(30L, 19L, "iota");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(19L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/iota/issues",
            new { title = new string('a', 201) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("issues:issue:title:required_or_too_long");
    }

    [Fact]
    public async Task CreateIssue_WithDescription10001Chars_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(20L, "val4@example.com", "Val User 4");
        var project = MakeProject(31L, 20L, "kappa");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(20L));

        var response = await client.PostAsJsonAsync(
            "/api/projects/kappa/issues",
            new { title = "Valid Title", description = new string('d', 10001) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("issues:issue:description:too_long");
    }

    [Fact]
    public async Task CreateIssue_With21LabelIds_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(21L, "val5@example.com", "Val User 5");
        var project = MakeProject(32L, 21L, "lambda");
        var label = new Label
        {
            Id = 400L,
            ProjectId = 32L,
            Name = "tag",
            Color = "#00ff00",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.Database.Save(user, project, label);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(21L));

        // Validator checks length (≤20) before any DB lookup — 21 identical encoded IDs triggers the rule
        var tooManyIds = Enumerable.Repeat(IdEncoding.Encode(400L), 21).ToArray();

        var response = await client.PostAsJsonAsync(
            "/api/projects/lambda/issues",
            new { title = "Too Many Labels", labelIds = tooManyIds },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("issues:issue:labels:too_many");
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly CreateIssue.RequestValidator _validator = new();

        [Fact]
        public void Title_empty_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "", null, null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Title)
                .WithErrorCode("issues:issue:title:required_or_too_long");
        }

        [Fact]
        public void Title_201_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", new string('a', 201), null, null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Title)
                .WithErrorCode("issues:issue:title:required_or_too_long");
        }

        [Fact]
        public void Title_200_chars_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", new string('a', 200), null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Priority_invalid_value_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, "invalid", null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Priority)
                .WithErrorCode("issues:issue:priority:invalid");
        }

        [Fact]
        public void Priority_null_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Priority);
        }

        [Fact]
        public void Priority_high_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, "high", null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Priority);
        }

        [Fact]
        public void LabelIds_length_21_fails_with_correct_error_code()
        {
            var ids = Enumerable.Repeat("someid", 21).ToArray();
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, ids, null));
            result.ShouldHaveValidationErrorFor(x => x.LabelIds)
                .WithErrorCode("issues:issue:labels:too_many");
        }

        [Fact]
        public void LabelIds_null_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.LabelIds);
        }

        [Fact]
        public void Description_10001_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", new string('d', 10001), null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Description)
                .WithErrorCode("issues:issue:description:too_long");
        }

        [Fact]
        public void Description_null_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void AcceptanceCriteria_10001_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, null, new string('x', 10001)));
            result.ShouldHaveValidationErrorFor(x => x.AcceptanceCriteria)
                .WithErrorCode("issues:issue:acceptance_criteria:too_long");
        }

        [Fact]
        public void AcceptanceCriteria_null_passes()
        {
            var result = _validator.TestValidate(
                new CreateIssue.Request("slug", "Valid Title", null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.AcceptanceCriteria);
        }
    }
}
