using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class GetProjectTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email, string name) => new()
    {
        Id = id, Email = email, PasswordHash = "hashed", Name = name, CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Project MakeProject(long id, string slug, string name, long ownerId) => new()
    {
        Id = id, Slug = slug, Name = name, OwnerId = ownerId,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };

    // -------------------------------------------------------------------------
    // Happy path: owner retrieves their own project
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_200_with_full_dto_when_project_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = MakeUser(1L, "owner@example.com", "Alice Owner");
        var project = MakeProject(100L, "my-project", "My Project", 1L);
        project.Description = "A useful project";
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1L));

        var response = await client.GetAsync("/api/projects/my-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetProject.Response>(ct);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("my-project");
        body.Name.Should().Be("My Project");
        body.Description.Should().Be("A useful project");
        body.OwnerId.Should().Be(IdEncoding.Encode(1L));
        body.OwnerName.Should().Be("Alice Owner");
        IdEncoding.TryDecode(body.Id, out _).Should().BeTrue();
        body.CreatedAt.Should().NotBe(default);
        body.UpdatedAt.Should().NotBe(default);
    }

    // -------------------------------------------------------------------------
    // Unknown slug → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_404_when_slug_does_not_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(2L, "user2@example.com", "Bob User");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(2L));

        var response = await client.GetAsync("/api/projects/nonexistent", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:not_found");
    }

    // -------------------------------------------------------------------------
    // Non-owner reader: read is open to all authenticated users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_200_when_authenticated_non_owner_reads_project()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = MakeUser(3L, "owner3@example.com", "Carol Owner");
        var reader = MakeUser(4L, "reader4@example.com", "Dave Reader");
        var project = MakeProject(200L, "shared-project", "Shared Project", 3L);
        await fixture.Database.Save(owner, reader, project);

        // Dave is the caller, not the owner
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(4L));

        var response = await client.GetAsync("/api/projects/shared-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetProject.Response>(ct);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("shared-project");
        body.OwnerId.Should().Be(IdEncoding.Encode(3L));
        body.OwnerName.Should().Be("Carol Owner");
    }
}
