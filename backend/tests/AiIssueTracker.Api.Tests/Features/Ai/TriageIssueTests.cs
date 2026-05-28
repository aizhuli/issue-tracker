using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Ai;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Ai;

[Collection(AiTestsCollection.Name)]
public class TriageIssueTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

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
            Name = "My Project",
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Issue MakeIssue(long id, long projectId, long reporterId, int number, string title,
        IssuePriority priority = IssuePriority.Medium, string? description = null) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = title,
            Description = description,
            Status = IssueStatus.Backlog,
            Priority = priority,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Label MakeLabel(long id, long projectId, string name, string color = "#ff0000") =>
        new Label
        {
            Id = id,
            ProjectId = projectId,
            Name = name,
            Color = color,
            CreatedAt = DateTimeOffset.UtcNow,
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

    // ── 1. Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Triage_HappyPath_Returns200WithParsedSuggestion()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(100L, "happy@example.com", "Happy User");
        var project = MakeProject(200L, 100L, "happy-proj");
        var label   = MakeLabel(300L, 200L, "bug");
        var issue   = MakeIssue(400L, 200L, 100L, 1, "Something is broken");
        await fixture.Database.Save(user, project, label, issue);

        fixture.ChatClient.RespondWithJson(
            """{"priority":"high","labels":["bug"],"acceptanceCriteria":"- [ ] It works"}""");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(100L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/happy-proj/issues/1/ai/triage",
            new { title = "Test", description = (string?)null },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TriageIssue.TriageSuggestion>(ct);
        body.Should().NotBeNull();
        body!.Priority.Should().Be("high");
        body.Labels.Should().HaveCount(1);
        body.Labels[0].Name.Should().Be("bug");
        body.AcceptanceCriteria.Should().Be("- [ ] It works");
    }

    // ── 2. Label resolution — real labels returned, hallucinated dropped ──────

    [Fact]
    public async Task Triage_LabelResolution_DropsHallucinatedLabels()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(101L, "labels@example.com", "Labels User");
        var project = MakeProject(201L, 101L, "labels-proj");
        var bugLabel     = MakeLabel(301L, 201L, "bug");
        var featureLabel = MakeLabel(302L, 201L, "feature");
        var issue   = MakeIssue(401L, 201L, 101L, 1, "Label test issue");
        await fixture.Database.Save(user, project, bugLabel, featureLabel, issue);

        fixture.ChatClient.RespondWithJson(
            """{"priority":"medium","labels":["bug","hallucinated","feature"],"acceptanceCriteria":"criteria"}""");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(101L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/labels-proj/issues/1/ai/triage",
            new { title = "Label test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TriageIssue.TriageSuggestion>(ct);
        body.Should().NotBeNull();
        body!.Labels.Should().HaveCount(2);
        body.Labels.Select(l => l.Name).Should().Contain("bug");
        body.Labels.Select(l => l.Name).Should().Contain("feature");
        body.Labels.Select(l => l.Name).Should().NotContain("hallucinated");
    }

    // ── 3. Priority clamp — off-list priority falls back to issue's priority ──

    [Fact]
    public async Task Triage_PriorityClamp_FallsBackToIssuePriority()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(102L, "clamp@example.com", "Clamp User");
        var project = MakeProject(202L, 102L, "clamp-proj");
        var issue   = MakeIssue(402L, 202L, 102L, 1, "Clamp issue", priority: IssuePriority.High);
        await fixture.Database.Save(user, project, issue);

        fixture.ChatClient.RespondWithJson(
            """{"priority":"invalid-value","labels":[],"acceptanceCriteria":"criteria"}""");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(102L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/clamp-proj/issues/1/ai/triage",
            new { title = "Clamp test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TriageIssue.TriageSuggestion>(ct);
        body.Should().NotBeNull();
        body!.Priority.Should().Be("high");
    }

    // ── 4. Invalid JSON → garbage after retry → 502 ───────────────────────────

    [Fact]
    public async Task Triage_InvalidJson_Returns502WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(103L, "invalid@example.com", "Invalid User");
        var project = MakeProject(203L, 103L, "invalid-proj");
        var issue   = MakeIssue(403L, 203L, 103L, 1, "Invalid JSON issue");
        await fixture.Database.Save(user, project, issue);

        // The fake keeps _nextJson across calls, so both attempts get garbage
        fixture.ChatClient.RespondWithRaw("not-valid-json");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(103L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/invalid-proj/issues/1/ai/triage",
            new { title = "Invalid test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("ai:triage:llm:invalid_response");
        fixture.ChatClient.CallCount.Should().Be(2); // handler retries once on parse failure
    }

    // ── 5. LLM throws → 502 unavailable ──────────────────────────────────────

    [Fact]
    public async Task Triage_LlmThrows_Returns502Unavailable()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(104L, "throw@example.com", "Throw User");
        var project = MakeProject(204L, 104L, "throw-proj");
        var issue   = MakeIssue(404L, 204L, 104L, 1, "Throw issue");
        await fixture.Database.Save(user, project, issue);

        fixture.ChatClient.ThrowOnNextCall();

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(104L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/throw-proj/issues/1/ai/triage",
            new { title = "Throw test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("ai:triage:llm:unavailable");
    }

    // ── 6. 404: unknown project slug ──────────────────────────────────────────

    [Fact]
    public async Task Triage_UnknownSlug_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = MakeUser(105L, "noproject@example.com", "No Project User");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(105L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/nonexistent/issues/1/ai/triage",
            new { title = "Test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:not_found");
    }

    // ── 7. 404: unknown issue number ──────────────────────────────────────────

    [Fact]
    public async Task Triage_UnknownIssueNumber_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(106L, "noissue@example.com", "No Issue User");
        var project = MakeProject(206L, 106L, "noissue-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(106L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/noissue-proj/issues/999/ai/triage",
            new { title = "Test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:not_found");
    }

    // ── 8. Validation: empty title → 400 ─────────────────────────────────────

    [Fact]
    public async Task Triage_EmptyTitle_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(107L, "emptytitle@example.com", "Empty Title User");
        var project = MakeProject(207L, 107L, "emptytitle-proj");
        var issue   = MakeIssue(407L, 207L, 107L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(107L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/emptytitle-proj/issues/1/ai/triage",
            new { title = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("ai:triage:title:required_or_too_long");
    }

    // ── 9. Validation: over-long description → 400 ───────────────────────────

    [Fact]
    public async Task Triage_OverLongDescription_Returns400WithCorrectErrorCode()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(108L, "longdesc@example.com", "Long Desc User");
        var project = MakeProject(208L, 108L, "longdesc-proj");
        var issue   = MakeIssue(408L, 208L, 108L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(108L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/longdesc-proj/issues/1/ai/triage",
            new { title = "T", description = new string('d', 10001) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("ai:triage:description:too_long");
    }

    // ── 10. Live values used — prompt contains request body, not stale DB text ─

    [Fact]
    public async Task Triage_LiveValues_PromptContainsRequestBodyNotDbText()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(109L, "live@example.com", "Live User");
        var project = MakeProject(209L, 109L, "live-proj");
        var issue   = MakeIssue(409L, 209L, 109L, 1, "DB Title", description: "DB description");
        await fixture.Database.Save(user, project, issue);

        // This test checks prompt content, not response parsing — set an explicit fake response.
        fixture.ChatClient.RespondWithJson("""{"priority":"medium","labels":[],"acceptanceCriteria":"AC text"}""");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(109L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/live-proj/issues/1/ai/triage",
            new { title = "Live Title", description = "Live desc" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lastMessages = fixture.ChatClient.LastMessages;
        lastMessages.Should().NotBeNull();

        var allText = string.Join("\n", lastMessages!.Select(m => m.Text ?? ""));
        allText.Should().Contain("Live Title");
        allText.Should().Contain("Live desc");
        allText.Should().NotContain("DB Title");
    }

    // ── 11. No writes — DB row unchanged after triage ─────────────────────────

    [Fact]
    public async Task Triage_NoWrites_IssueRowUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(110L, "nowrites@example.com", "No Writes User");
        var project = MakeProject(210L, 110L, "nowrites-proj");
        var issue   = MakeIssue(410L, 210L, 110L, 1, "No Writes Issue", priority: IssuePriority.Low);
        await fixture.Database.Save(user, project, issue);

        fixture.ChatClient.RespondWithJson(
            """{"priority":"urgent","labels":[],"acceptanceCriteria":"AI-generated criteria"}""");

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(110L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/nowrites-proj/issues/1/ai/triage",
            new { title = "No writes test" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Response says urgent, but DB row must remain untouched
        var body = await response.Content.ReadFromJsonAsync<TriageIssue.TriageSuggestion>(ct);
        body!.Priority.Should().Be("urgent");

        var reloaded = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 410L, ct);
        reloaded.Should().NotBeNull();
        reloaded!.Priority.Should().Be(IssuePriority.Low);
        reloaded.AcceptanceCriteria.Should().BeNull();
    }
}
