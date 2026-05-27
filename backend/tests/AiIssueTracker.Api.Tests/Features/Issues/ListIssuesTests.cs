using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class ListIssuesTests(TestFixture fixture) : IAsyncLifetime
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

    private static Issue MakeIssue(
        long id,
        long projectId,
        long reporterId,
        int number,
        string title,
        IssueStatus status = IssueStatus.Backlog,
        IssuePriority priority = IssuePriority.Medium,
        long? assigneeId = null,
        string? description = null,
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
            Description = description,
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
    // Pagination round-trip: 25 backlog issues → 10 + 10 + 5, no duplicates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_PaginationRoundTrip_Returns25IssuesAcrossThreePages()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(100L, "pager@example.com", "Pager");
        var project = MakeProject(200L, 100L, "page-proj");
        var baseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var issues = Enumerable.Range(1, 25)
            .Select(i => MakeIssue(300L + i, 200L, 100L, i, $"Issue {i:D2}", createdAt: baseTime.AddSeconds(i)))
            .ToArray();
        await fixture.Database.Save(user, project);
        await fixture.Database.Save(issues);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var page1Response = await client.GetAsync("/api/projects/page-proj/issues?status=backlog&maxPageSize=10", ct);
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);
        page1!.Items.Should().HaveCount(10);
        page1.NextPageToken.Should().NotBeNull();

        var page2Response = await client.GetAsync(
            $"/api/projects/page-proj/issues?status=backlog&maxPageSize=10&pageToken={Uri.EscapeDataString(page1.NextPageToken!)}", ct);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);
        page2!.Items.Should().HaveCount(10);
        page2.NextPageToken.Should().NotBeNull();

        var page3Response = await client.GetAsync(
            $"/api/projects/page-proj/issues?status=backlog&maxPageSize=10&pageToken={Uri.EscapeDataString(page2.NextPageToken!)}", ct);
        page3Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);
        page3!.Items.Should().HaveCount(5);
        page3.NextPageToken.Should().BeNull();

        var allIds = page1.Items.Select(i => i.Id)
            .Concat(page2.Items.Select(i => i.Id))
            .Concat(page3.Items.Select(i => i.Id))
            .ToList();
        allIds.Should().HaveCount(25);
        allIds.Distinct().Should().HaveCount(25);
    }

    // -------------------------------------------------------------------------
    // q=login: matches title substring AND description substring
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_QFilter_MatchesTitleAndDescriptionSubstring()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(100L, "filter@example.com", "Filter User");
        var project = MakeProject(200L, 100L, "q-proj");
        var loginFails   = MakeIssue(300L, 200L, 100L, 1, "Login fails");
        var logoutButton = MakeIssue(301L, 200L, 100L, 2, "Logout button");
        var crashOnSave  = MakeIssue(302L, 200L, 100L, 3, "Crash on save",
            description: "This crash happens after login");
        await fixture.Database.Save(user, project, loginFails, logoutButton, crashOnSave);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync("/api/projects/q-proj/issues?status=backlog&q=login", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);

        result!.Items.Should().HaveCount(2);
        var numbers = result.Items.Select(i => i.Number).ToList();
        numbers.Should().Contain(1); // "Login fails" — title match
        numbers.Should().Contain(3); // "Crash on save" — description match
        numbers.Should().NotContain(2); // "Logout button" — no match
    }

    // -------------------------------------------------------------------------
    // priority=high&priority=urgent: OR-filter returns only those two priorities
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_PriorityFilter_OrFiltersHighAndUrgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(100L, "priority@example.com", "Priority User");
        var project = MakeProject(200L, 100L, "prio-proj");
        var low    = MakeIssue(300L, 200L, 100L, 1, "Low",    priority: IssuePriority.Low);
        var medium = MakeIssue(301L, 200L, 100L, 2, "Medium", priority: IssuePriority.Medium);
        var high   = MakeIssue(302L, 200L, 100L, 3, "High",   priority: IssuePriority.High);
        var urgent = MakeIssue(303L, 200L, 100L, 4, "Urgent", priority: IssuePriority.Urgent);
        await fixture.Database.Save(user, project, low, medium, high, urgent);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync(
            "/api/projects/prio-proj/issues?status=backlog&priority=high&priority=urgent", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);

        result!.Items.Should().HaveCount(2);
        var numbers = result.Items.Select(i => i.Number).ToList();
        numbers.Should().Contain(3); // high
        numbers.Should().Contain(4); // urgent
        numbers.Should().NotContain(1); // low — excluded
        numbers.Should().NotContain(2); // medium — excluded
    }

    // -------------------------------------------------------------------------
    // assignee=unassigned: returns only null-assignee rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_AssigneeUnassigned_ReturnsOnlyNullAssigneeRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(100L, "reporter@example.com", "Reporter");
        var assignee = MakeUser(101L, "assignee@example.com", "Assignee");
        var project  = MakeProject(200L, 100L, "assign-proj");
        var unassigned = MakeIssue(300L, 200L, 100L, 1, "Unassigned");
        var assigned   = MakeIssue(301L, 200L, 100L, 2, "Assigned", assigneeId: 101L);
        await fixture.Database.Save(reporter, assignee, project, unassigned, assigned);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync(
            "/api/projects/assign-proj/issues?status=backlog&assignee=unassigned", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);

        result!.Items.Should().HaveCount(1);
        result.Items[0].Number.Should().Be(1);
        result.Items[0].Assignee.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // assignee={uid}&assignee=unassigned: ORs assigned user and unassigned rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_AssigneeUserIdPlusUnassigned_OrsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(100L, "reporter2@example.com", "Reporter");
        var userB    = MakeUser(101L, "userb@example.com", "User B");
        var userC    = MakeUser(102L, "userc@example.com", "User C");
        var project  = MakeProject(200L, 100L, "or-assign-proj");
        var unassigned = MakeIssue(300L, 200L, 100L, 1, "Unassigned");
        var assignedB  = MakeIssue(301L, 200L, 100L, 2, "Assigned B", assigneeId: 101L);
        var assignedC  = MakeIssue(302L, 200L, 100L, 3, "Assigned C", assigneeId: 102L);
        await fixture.Database.Save(reporter, userB, userC, project, unassigned, assignedB, assignedC);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var encodedUserB = Uri.EscapeDataString(IdEncoding.Encode(101L));
        var response = await client.GetAsync(
            $"/api/projects/or-assign-proj/issues?status=backlog&assignee={encodedUserB}&assignee=unassigned", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);

        result!.Items.Should().HaveCount(2);
        var numbers = result.Items.Select(i => i.Number).ToList();
        numbers.Should().Contain(1); // unassigned
        numbers.Should().Contain(2); // assigned to B
        numbers.Should().NotContain(3); // assigned to C — excluded
    }

    // -------------------------------------------------------------------------
    // Stable sort: same CreatedAt → descending Id tiebreaker
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_StableSort_TiebreaksOnDescendingId()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(100L, "sorter@example.com", "Sorter");
        var project = MakeProject(200L, 100L, "sort-proj");
        var sameTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var issueA = MakeIssue(300L, 200L, 100L, 1, "Issue A", createdAt: sameTime);
        var issueB = MakeIssue(301L, 200L, 100L, 2, "Issue B", createdAt: sameTime);
        var issueC = MakeIssue(302L, 200L, 100L, 3, "Issue C", createdAt: sameTime);
        await fixture.Database.Save(user, project, issueA, issueB, issueC);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync("/api/projects/sort-proj/issues?status=backlog", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);

        result!.Items.Should().HaveCount(3);
        // Descending Id: 302L > 301L > 300L → numbers 3, 2, 1
        result.Items[0].Number.Should().Be(3);
        result.Items[1].Number.Should().Be(2);
        result.Items[2].Number.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // Token reuse with changed q → 400 paging:validation:page_token_invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_TokenReuse_WithDifferentQ_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(100L, "reuser@example.com", "Re-user");
        var project = MakeProject(200L, 100L, "token-proj");
        var baseTime = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var issues = Enumerable.Range(1, 10)
            .Select(i => MakeIssue(300L + i, 200L, 100L, i, $"Login issue {i}", createdAt: baseTime.AddSeconds(i)))
            .ToArray();
        await fixture.Database.Save(user, project);
        await fixture.Database.Save(issues);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var page1Response = await client.GetAsync(
            "/api/projects/token-proj/issues?status=backlog&q=Login&maxPageSize=5", ct);
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PageResponse<IssueSummary>>(ct);
        page1!.NextPageToken.Should().NotBeNull();

        var replayUrl =
            $"/api/projects/token-proj/issues?status=backlog&q=Different&pageToken={Uri.EscapeDataString(page1.NextPageToken!)}&maxPageSize=5";
        var replayResponse = await client.GetAsync(replayUrl, ct);

        replayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await replayResponse.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("paging:validation:page_token_invalid");
    }

    // -------------------------------------------------------------------------
    // Missing status → 400 with issues:issue:status:invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_MissingStatus_Returns400WithValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync("/api/projects/any-proj/issues", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("issues:issue:status:invalid");
    }

    // -------------------------------------------------------------------------
    // maxPageSize=0 → 400 paging:validation:max_page_size_invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_MaxPageSizeZero_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync("/api/projects/any-proj/issues?status=backlog&maxPageSize=0", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("paging:validation:max_page_size_invalid");
    }

    // -------------------------------------------------------------------------
    // maxPageSize=101 → 400 paging:validation:max_page_size_invalid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListIssues_MaxPageSize101_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));

        var response = await client.GetAsync("/api/projects/any-proj/issues?status=backlog&maxPageSize=101", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("paging:validation:max_page_size_invalid");
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly ListIssues.RequestValidator _validator = new();

        [Fact]
        public void Status_null_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", null, null, null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_empty_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "", null, null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_invalid_value_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "invalid-status", null, null, null, null, null));
            result.ShouldHaveValidationErrorFor(x => x.Status)
                .WithErrorCode("issues:issue:status:invalid");
        }

        [Fact]
        public void Status_backlog_passes()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "backlog", null, null, null, null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Status);
        }

        [Fact]
        public void Status_all_valid_enum_values_pass()
        {
            foreach (var status in new[] { "backlog", "todo", "in-progress", "in-review", "done" })
            {
                var result = _validator.TestValidate(
                    new ListIssues.Request("slug", status, null, null, null, null, null));
                result.ShouldNotHaveValidationErrorFor(x => x.Status);
            }
        }

        [Fact]
        public void Priority_invalid_value_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "backlog", null, null, null, ["nope"], null));
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorCode == "issues:issue:priority:invalid");
        }

        [Fact]
        public void Priority_valid_value_passes()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "backlog", null, null, null, ["high"], null));
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Priority_null_passes()
        {
            var result = _validator.TestValidate(
                new ListIssues.Request("slug", "backlog", null, null, null, null, null));
            result.IsValid.Should().BeTrue();
        }
    }
}
