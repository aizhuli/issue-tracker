using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Issues;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Tests.Features.Issues;

[Collection(IssuesTestsCollection.Name)]
public class DeleteIssueTests(TestFixture fixture) : IAsyncLifetime
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

    private static Issue MakeIssue(long id, long projectId, long reporterId, int number) =>
        new()
        {
            Id = id,
            ProjectId = projectId,
            ReporterId = reporterId,
            Number = number,
            Title = "Issue",
            Status = IssueStatus.Backlog,
            Priority = IssuePriority.Medium,
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
    // 204 by reporter; row gone
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteIssue_ByReporter_Returns204AndRemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var project  = MakeProject(20L, 10L, "del-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1);
        await fixture.Database.Save(reporter, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync("/api/projects/del-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 30L, ct);
        gone.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // 204 by project owner (non-reporter); row gone
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteIssue_ByProjectOwnerNotReporter_Returns204AndRemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var owner    = MakeUser(11L, "owner@example.com", "Owner");
        var project  = MakeProject(20L, 11L, "own-del-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1);
        await fixture.Database.Save(reporter, owner, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(11L));
        var response = await client.DeleteAsync("/api/projects/own-del-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 30L, ct);
        gone.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // 403 by anyone else; row untouched
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteIssue_ByUnrelatedUser_Returns403AndLeavesRowIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter  = MakeUser(10L, "reporter@example.com", "Reporter");
        var owner     = MakeUser(11L, "owner@example.com", "Owner");
        var bystander = MakeUser(12L, "bystander@example.com", "Bystander");
        var project   = MakeProject(20L, 11L, "403-proj");
        var issue     = MakeIssue(30L, 20L, 10L, 1);
        await fixture.Database.Save(reporter, owner, bystander, project, issue);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(12L));
        var response = await client.DeleteAsync("/api/projects/403-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("issues:issue:delete:forbidden");

        var still = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 30L, ct);
        still.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Cascade: 2 comments + 2 label links removed; labels themselves untouched
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteIssue_CascadesCommentsAndLabelLinks_ButLeavesLabelsIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        var reporter = MakeUser(10L, "reporter@example.com", "Reporter");
        var project  = MakeProject(20L, 10L, "casc-proj");
        var issue    = MakeIssue(30L, 20L, 10L, 1);
        var labelA   = new Label { Id = 40L, ProjectId = 20L, Name = "alpha", Color = "#aaaaaa", CreatedAt = DateTimeOffset.UtcNow };
        var labelB   = new Label { Id = 41L, ProjectId = 20L, Name = "beta",  Color = "#bbbbbb", CreatedAt = DateTimeOffset.UtcNow };
        var linkA    = new IssueLabel { IssueId = 30L, LabelId = 40L };
        var linkB    = new IssueLabel { IssueId = 30L, LabelId = 41L };
        var comment1 = MakeComment(50L, 30L, 10L);
        var comment2 = MakeComment(51L, 30L, 10L);
        await fixture.Database.Save(reporter, project, issue, labelA, labelB, linkA, linkB, comment1, comment2);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.DeleteAsync("/api/projects/casc-proj/issues/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Issue row gone
        var gone = await fixture.Database.SingleOrDefault<Issue>(i => i.Id == 30L, ct);
        gone.Should().BeNull();

        // Comments cascaded
        var commentCount = await fixture.Database.Execute<int>(
            db => db.Comments.CountAsync(c => c.IssueId == 30L, ct));
        commentCount.Should().Be(0);

        // IssueLabel rows cascaded
        var linkCount = await fixture.Database.Execute<int>(
            db => db.IssueLabels.CountAsync(il => il.IssueId == 30L, ct));
        linkCount.Should().Be(0);

        // Labels themselves untouched
        var labelCount = await fixture.Database.Execute<int>(
            db => db.Labels.CountAsync(l => l.Id == 40L || l.Id == 41L, ct));
        labelCount.Should().Be(2);
    }
}
