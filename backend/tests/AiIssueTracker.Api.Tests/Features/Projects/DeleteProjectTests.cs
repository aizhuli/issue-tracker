using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class DeleteProjectTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email, string name) => new()
    {
        Id = id,
        Email = email,
        PasswordHash = "hashed",
        Name = name,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Project MakeProject(long id, string slug, string name, long ownerId) => new()
    {
        Id = id,
        Slug = slug,
        Name = name,
        OwnerId = ownerId,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    // -------------------------------------------------------------------------
    // Happy path: owner deletes their project → 204, row gone
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_204_and_remove_project_when_owner_deletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = MakeUser(1L, "owner@example.com", "Owner");
        var project = MakeProject(100L, "my-project", "My Project", 1L);
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1L));

        var response = await client.DeleteAsync("/api/projects/my-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await fixture.Database.SingleOrDefault<Project>(p => p.Id == 100L, ct);
        gone.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Cascade: deleting a project removes associated Issues and Labels
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_cascade_delete_issues_and_labels_when_project_is_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = MakeUser(2L, "owner2@example.com", "Owner Two");
        var project = MakeProject(200L, "cascade-project", "Cascade Project", 2L);
        await fixture.Database.Save(owner, project);

        var issue1 = new Issue { Id = 10L, ProjectId = 200L, ReporterId = 2L, Number = 1 };
        var issue2 = new Issue { Id = 11L, ProjectId = 200L, ReporterId = 2L, Number = 2 };
        var label1 = new Label { Id = 20L, ProjectId = 200L };
        await fixture.Database.Save(issue1, issue2, label1);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(2L));

        var response = await client.DeleteAsync("/api/projects/cascade-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await fixture.Database.Execute(async db =>
        {
            var issueCount = await db.Issues.CountAsync(i => i.ProjectId == 200L);
            issueCount.Should().Be(0);

            var labelCount = await db.Labels.CountAsync(l => l.ProjectId == 200L);
            labelCount.Should().Be(0);
        });
    }

    // -------------------------------------------------------------------------
    // Non-owner → 403 with correct errorCode; DB row untouched
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_403_and_leave_project_intact_when_non_owner_deletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = MakeUser(3L, "owner3@example.com", "Owner Three");
        var other = MakeUser(4L, "other4@example.com", "Other User");
        var project = MakeProject(300L, "owned-project", "Owned Project", 3L);
        await fixture.Database.Save(owner, other, project);

        // Caller is "other", not the owner
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(4L));

        var response = await client.DeleteAsync("/api/projects/owned-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:delete:forbidden");

        // Row must still exist
        var stillThere = await fixture.Database.SingleOrDefault<Project>(p => p.Id == 300L, ct);
        stillThere.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Unknown slug → 404 with correct errorCode
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_404_when_slug_does_not_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(5L, "user5@example.com", "User Five");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(5L));

        var response = await client.DeleteAsync("/api/projects/nonexistent-slug", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:not_found");
    }
}
