using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Comments;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Comments;

[Collection(CommentsTestsCollection.Name)]
public class CommentsTests(TestFixture fixture) : IAsyncLifetime
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

    private static Issue MakeIssue(long id, long projectId, long reporterId) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = 1,
            Title = "Issue",
            Status = IssueStatus.Backlog,
            Priority = IssuePriority.Medium,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Comment MakeComment(long id, long issueId, long authorId,
        string body = "A comment", DateTimeOffset? createdAt = null)
    {
        var ts = createdAt ?? DateTimeOffset.UtcNow;
        return new Comment
        {
            Id = id,
            IssueId = issueId,
            AuthorId = authorId,
            Body = body,
            CreatedAt = ts,
            UpdatedAt = ts,
        };
    }

    // =========================================================================
    // CreateComment
    // =========================================================================

    [Fact]
    public async Task CreateComment_ValidBody_Returns201WithAuthorIdAndEditedFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "cc-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PostAsJsonAsync(
            "/api/projects/cc-proj/issues/1/comments",
            new { body = "Hello world" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentDto>(ct);
        body.Should().NotBeNull();
        body!.AuthorId.Should().Be(IdEncoding.Encode(10L));
        body.Body.Should().Be("Hello world");
        body.Edited.Should().BeFalse();
    }

    // =========================================================================
    // ListComments
    // =========================================================================

    [Fact]
    public async Task ListComments_SeedsAscOrdering_ReturnsCommentsOldestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var base_ = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "lc-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        await fixture.Database.Save(user, project, issue,
            MakeComment(50L, 30L, 10L, "First",  createdAt: base_.AddSeconds(1)),
            MakeComment(51L, 30L, 10L, "Second", createdAt: base_.AddSeconds(2)),
            MakeComment(52L, 30L, 10L, "Third",  createdAt: base_.AddSeconds(3)));

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync(
            "/api/projects/lc-proj/issues/1/comments", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PageResponse<CommentDto>>(ct);
        result!.Items.Should().HaveCount(3);
        result.Items[0].Body.Should().Be("First");
        result.Items[1].Body.Should().Be("Second");
        result.Items[2].Body.Should().Be("Third");
    }

    [Fact]
    public async Task ListComments_PaginationRoundTrip_NoGapsOrDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var base_ = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "pg-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        var comments = Enumerable.Range(1, 5)
            .Select(i => MakeComment(50L + i, 30L, 10L, $"Comment {i}",
                createdAt: base_.AddSeconds(i)))
            .ToArray();
        await fixture.Database.Save(user, project, issue);
        await fixture.Database.Save(comments);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));

        var page1 = await client.GetAsync(
            "/api/projects/pg-proj/issues/1/comments?maxPageSize=3", ct);
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var r1 = await page1.Content.ReadFromJsonAsync<PageResponse<CommentDto>>(ct);
        r1!.Items.Should().HaveCount(3);
        r1.NextPageToken.Should().NotBeNull();

        var page2 = await client.GetAsync(
            $"/api/projects/pg-proj/issues/1/comments?maxPageSize=3&pageToken={Uri.EscapeDataString(r1.NextPageToken!)}", ct);
        page2.StatusCode.Should().Be(HttpStatusCode.OK);
        var r2 = await page2.Content.ReadFromJsonAsync<PageResponse<CommentDto>>(ct);
        r2!.Items.Should().HaveCount(2);
        r2.NextPageToken.Should().BeNull();

        var allIds = r1.Items.Select(c => c.Id).Concat(r2.Items.Select(c => c.Id)).ToList();
        allIds.Should().HaveCount(5);
        allIds.Distinct().Should().HaveCount(5);
    }

    // =========================================================================
    // UpdateComment
    // =========================================================================

    [Fact]
    public async Task UpdateComment_ByAuthor_Returns200WithEditedTrueAndBumpedUpdatedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var past = DateTimeOffset.UtcNow.AddSeconds(-2);
        var user    = MakeUser(10L, "author@example.com", "Author");
        var project = MakeProject(20L, 10L, "uc-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        var comment = MakeComment(50L, 30L, 10L, "Original", createdAt: past);
        await fixture.Database.Save(user, project, issue, comment);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/uc-proj/issues/1/comments/{IdEncoding.Encode(50L)}",
            new { body = "Edited body" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentDto>(ct);
        body.Should().NotBeNull();
        body!.Body.Should().Be("Edited body");
        body.Edited.Should().BeTrue();
        body.UpdatedAt.Should().BeAfter(past);
    }

    [Fact]
    public async Task UpdateComment_ByNonAuthor_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var author  = MakeUser(10L, "author@example.com", "Author");
        var other   = MakeUser(11L, "other@example.com", "Other");
        var project = MakeProject(20L, 10L, "uc403-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        var comment = MakeComment(50L, 30L, 10L, "Original");
        await fixture.Database.Save(author, other, project, issue, comment);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.PutAsJsonAsync(
            $"/api/projects/uc403-proj/issues/1/comments/{IdEncoding.Encode(50L)}",
            new { body = "Hacked" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("comments:comment:update:forbidden");
    }

    // =========================================================================
    // DeleteComment
    // =========================================================================

    [Fact]
    public async Task DeleteComment_ByAuthor_Returns204AndRemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "author@example.com", "Author");
        var project = MakeProject(20L, 10L, "dc-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        var comment = MakeComment(50L, 30L, 10L);
        await fixture.Database.Save(user, project, issue, comment);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync(
            $"/api/projects/dc-proj/issues/1/comments/{IdEncoding.Encode(50L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await fixture.Database.SingleOrDefault<Comment>(c => c.Id == 50L, ct);
        gone.Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_ByNonAuthor_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var author  = MakeUser(10L, "author@example.com", "Author");
        var other   = MakeUser(11L, "other@example.com", "Other");
        var project = MakeProject(20L, 10L, "dc403-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        var comment = MakeComment(50L, 30L, 10L);
        await fixture.Database.Save(author, other, project, issue, comment);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.DeleteAsync(
            $"/api/projects/dc403-proj/issues/1/comments/{IdEncoding.Encode(50L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("comments:comment:delete:forbidden");
    }

    [Fact]
    public async Task DeleteComment_UnknownCommentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var user    = MakeUser(10L, "user@example.com", "User");
        var project = MakeProject(20L, 10L, "dc404-proj");
        var issue   = MakeIssue(30L, 20L, 10L);
        await fixture.Database.Save(user, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync(
            $"/api/projects/dc404-proj/issues/1/comments/{IdEncoding.Encode(99999L)}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("comments:comment:not_found");
    }

    // =========================================================================
    // Nested validator unit tests
    // =========================================================================

    public class ValidatorTests
    {
        private readonly CreateComment.RequestValidator _createValidator = new();
        private readonly UpdateComment.RequestValidator _updateValidator = new();

        [Fact]
        public void Create_Body_empty_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(new CreateComment.Request("slug", 1, ""));
            result.ShouldHaveValidationErrorFor(x => x.Body)
                .WithErrorCode("comments:comment:body:required_or_too_long");
        }

        [Fact]
        public void Create_Body_10001_chars_fails_with_correct_error_code()
        {
            var result = _createValidator.TestValidate(
                new CreateComment.Request("slug", 1, new string('x', 10001)));
            result.ShouldHaveValidationErrorFor(x => x.Body)
                .WithErrorCode("comments:comment:body:required_or_too_long");
        }

        [Fact]
        public void Create_Body_10000_chars_passes()
        {
            var result = _createValidator.TestValidate(
                new CreateComment.Request("slug", 1, new string('x', 10000)));
            result.ShouldNotHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Update_Body_empty_fails_with_correct_error_code()
        {
            var result = _updateValidator.TestValidate(
                new UpdateComment.Request("slug", 1, "commentid", ""));
            result.ShouldHaveValidationErrorFor(x => x.Body)
                .WithErrorCode("comments:comment:body:required_or_too_long");
        }

        [Fact]
        public void Update_Body_10001_chars_fails_with_correct_error_code()
        {
            var result = _updateValidator.TestValidate(
                new UpdateComment.Request("slug", 1, "commentid", new string('x', 10001)));
            result.ShouldHaveValidationErrorFor(x => x.Body)
                .WithErrorCode("comments:comment:body:required_or_too_long");
        }

        [Fact]
        public void Update_Body_10000_chars_passes()
        {
            var result = _updateValidator.TestValidate(
                new UpdateComment.Request("slug", 1, "commentid", new string('x', 10000)));
            result.ShouldNotHaveValidationErrorFor(x => x.Body);
        }
    }
}
