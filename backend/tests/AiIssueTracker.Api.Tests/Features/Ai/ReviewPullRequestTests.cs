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
public class ReviewPullRequestTests(TestFixture fixture) : IAsyncLifetime
{
    private const string ValidPrUrl = "https://github.com/owner/repo/pull/1";
    private const string ReviewText = "## Code Review\n\nThis PR looks good.";

    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static User MakeAiUser() =>
        new()
        {
            Id = SystemUsers.AiUserId,
            Email = "ai@system",
            PasswordHash = "!",
            Name = "AI",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static User MakeUser(long id, string email, string name = "User") =>
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
            Name = "Test Project",
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Issue MakeIssue(long id, long projectId, long reporterId, int number, string title,
        string? acceptanceCriteria = null) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = title,
            Status = IssueStatus.Backlog,
            Priority = IssuePriority.Medium,
            AcceptanceCriteria = acceptanceCriteria,
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

    // ── 1. Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewPr_HappyPath_Returns200AndPersistsComment()
    {
        var ct = TestContext.Current.CancellationToken;

        var aiUser  = MakeAiUser();
        var user    = MakeUser(500L, "happy@example.com");
        var project = MakeProject(600L, 500L, "review-proj");
        var issue   = MakeIssue(700L, 600L, 500L, 1, "Add login page");
        await fixture.Database.Save(aiUser, user, project, issue);

        fixture.ChatClient.RespondWithRaw(ReviewText);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(500L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/review-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<ReviewPullRequest.ReviewDto>(ct);
        dto.Should().NotBeNull();
        dto!.Body.Should().Be(ReviewText.Trim());
        dto.CommentId.Should().NotBeNullOrEmpty();

        var comment = await fixture.Database.SingleOrDefault<Comment>(c => c.IssueId == 700L, ct);
        comment.Should().NotBeNull();
        comment!.AuthorId.Should().Be(SystemUsers.AiUserId);
        comment.Body.Should().Be(ReviewText.Trim());
    }

    // ── 2. Acceptance criteria anchors system prompt ──────────────────────────

    [Fact]
    public async Task ReviewPr_WithAcceptanceCriteria_IncludesInSystemPrompt()
    {
        var ct = TestContext.Current.CancellationToken;

        var aiUser  = MakeAiUser();
        var user    = MakeUser(501L, "ac@example.com");
        var project = MakeProject(601L, 501L, "ac-proj");
        var issue   = MakeIssue(701L, 601L, 501L, 1, "Feature", "- [ ] It works\n- [ ] Tests pass");
        await fixture.Database.Save(aiUser, user, project, issue);

        fixture.ChatClient.RespondWithRaw(ReviewText);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(501L));
        await client.PostAsJsonAsync(
            "/api/projects/ac-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        var messages = fixture.ChatClient.LastMessages;
        messages.Should().NotBeNull();

        var allText = string.Join("\n", messages!.Select(m => m.Text ?? ""));
        allText.Should().Contain("- [ ] It works");
        allText.Should().Contain("- [ ] Tests pass");
        allText.Should().Contain("acceptance criteria");
    }

    // ── 3. No acceptance criteria → general review prompt ────────────────────

    [Fact]
    public async Task ReviewPr_NoAcceptanceCriteria_UsesGeneralPrompt()
    {
        var ct = TestContext.Current.CancellationToken;

        var aiUser  = MakeAiUser();
        var user    = MakeUser(502L, "noacc@example.com");
        var project = MakeProject(602L, 502L, "noacc-proj");
        var issue   = MakeIssue(702L, 602L, 502L, 1, "Fix bug", acceptanceCriteria: null);
        await fixture.Database.Save(aiUser, user, project, issue);

        fixture.ChatClient.RespondWithRaw(ReviewText);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(502L));
        await client.PostAsJsonAsync(
            "/api/projects/noacc-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        var messages = fixture.ChatClient.LastMessages;
        messages.Should().NotBeNull();

        var allText = string.Join("\n", messages!.Select(m => m.Text ?? ""));
        allText.Should().Contain("code quality");
        allText.Should().NotContain("acceptance criteria");
    }

    // ── 4. LLM throws → 502 unavailable ──────────────────────────────────────

    [Fact]
    public async Task ReviewPr_LlmThrows_Returns502Unavailable()
    {
        var ct = TestContext.Current.CancellationToken;

        var aiUser  = MakeAiUser();
        var user    = MakeUser(503L, "throw@example.com");
        var project = MakeProject(603L, 503L, "throw-proj");
        var issue   = MakeIssue(703L, 603L, 503L, 1, "Throw issue");
        await fixture.Database.Save(aiUser, user, project, issue);

        fixture.ChatClient.ThrowOnNextCall();

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(503L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/throw-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("ai:pr_review:llm:unavailable");
    }

    // ── 5. 404: unknown project slug ──────────────────────────────────────────

    [Fact]
    public async Task ReviewPr_UnknownProject_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = MakeUser(504L, "noproj@example.com");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(504L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/nonexistent/issues/1/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:not_found");
    }

    // ── 6. 404: unknown issue number ──────────────────────────────────────────

    [Fact]
    public async Task ReviewPr_UnknownIssue_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(505L, "noissue@example.com");
        var project = MakeProject(605L, 505L, "noissue-proj");
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(505L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/noissue-proj/issues/999/ai/review-pr",
            new { pullRequestUrl = ValidPrUrl },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:not_found");
    }

    // ── 7. Validation: empty URL → 400 required ───────────────────────────────

    [Fact]
    public async Task ReviewPr_EmptyUrl_Returns400WithRequiredError()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(506L, "emptyurl@example.com");
        var project = MakeProject(606L, 506L, "emptyurl-proj");
        var issue   = MakeIssue(706L, 606L, 506L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(506L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/emptyurl-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("ai:pr_review:pull_request_url:required");
    }

    // ── 8. Validation: non-GitHub URL format → 400 invalid ───────────────────

    [Fact]
    public async Task ReviewPr_InvalidUrl_Returns400WithInvalidError()
    {
        var ct = TestContext.Current.CancellationToken;

        var user    = MakeUser(507L, "badurl@example.com");
        var project = MakeProject(607L, 507L, "badurl-proj");
        var issue   = MakeIssue(707L, 607L, 507L, 1, "Issue");
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(507L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/badurl-proj/issues/1/ai/review-pr",
            new { pullRequestUrl = "https://gitlab.com/owner/repo/merge_requests/1" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("ai:pr_review:pull_request_url:invalid");
    }
}
