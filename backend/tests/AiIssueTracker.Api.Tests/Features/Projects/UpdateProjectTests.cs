using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class UpdateProjectTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static User MakeUser(long id, string email = "owner@example.com", string name = "Owner") =>
        new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hashed",
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Project MakeProject(long id, string slug, string name, long ownerId,
        DateTimeOffset? createdAt = null) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = name,
            OwnerId = ownerId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Validation errors have errorCode "common:validation:failed" at the top level.
    /// The per-field error codes live in extensions["errors"][fieldName][i]["code"].
    /// This helper extracts all leaf error codes from the nested errors dictionary.
    /// </summary>
    private static List<string> ExtractValidationErrorCodes(JsonDocument doc)
    {
        var codes = new List<string>();
        if (!doc.RootElement.TryGetProperty("errors", out var errors))
            return codes;

        foreach (var field in errors.EnumerateObject())
        {
            foreach (var entry in field.Value.EnumerateArray())
            {
                if (entry.TryGetProperty("code", out var code))
                    codes.Add(code.GetString()!);
            }
        }

        return codes;
    }

    // -------------------------------------------------------------------------
    // Happy path: owner updates their own project
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_200_with_updated_values_when_owner_updates_project()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed project with CreatedAt slightly in the past so UpdatedAt > CreatedAt is verifiable
        var pastTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        var owner = MakeUser(1L, "owner@example.com", "Alice Owner");
        var project = MakeProject(100L, "my-project", "Original Name", 1L, pastTime);
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/my-project",
            new { name = "New Name", description = "New desc" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UpdateProject.Response>(ct);
        body.Should().NotBeNull();
        body!.Name.Should().Be("New Name");
        body.Description.Should().Be("New desc");
        body.Slug.Should().Be("my-project");
        body.OwnerId.Should().Be(IdEncoding.Encode(1L));
        body.OwnerName.Should().Be("Alice Owner");

        // UpdatedAt must be strictly after CreatedAt (which was set to pastTime)
        (body.UpdatedAt > body.CreatedAt).Should().BeTrue();
    }

    [Fact]
    public async Task Should_persist_updated_values_and_keep_slug_and_owner_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;

        var pastTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        var owner = MakeUser(2L, "owner2@example.com", "Bob Owner");
        var project = MakeProject(200L, "stable-slug", "Before Update", 2L, pastTime);
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(2L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/stable-slug",
            new { name = "After Update", description = "Updated description" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read the row back and verify slug + ownerId are untouched, UpdatedAt advanced
        var row = await fixture.Database.SingleOrDefault<Project>(p => p.Id == 200L, ct);
        row.Should().NotBeNull();
        row!.Name.Should().Be("After Update");
        row.Description.Should().Be("Updated description");
        row.Slug.Should().Be("stable-slug");
        row.OwnerId.Should().Be(2L);
        (row.UpdatedAt > row.CreatedAt).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Non-owner → 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_403_and_leave_row_unchanged_when_non_owner_updates_project()
    {
        var ct = TestContext.Current.CancellationToken;

        var owner = MakeUser(3L, "owner3@example.com", "Carol Owner");
        var other = MakeUser(4L, "other4@example.com", "Dave Other");
        var project = MakeProject(300L, "protected-project", "Original Name", 3L);
        await fixture.Database.Save(owner, other, project);

        // Dave is NOT the owner
        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(4L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/protected-project",
            new { name = "Hijacked Name", description = "Hijacked desc" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:edit:forbidden");

        // DB row must be unchanged
        var row = await fixture.Database.SingleOrDefault<Project>(p => p.Id == 300L, ct);
        row.Should().NotBeNull();
        row!.Name.Should().Be("Original Name");
    }

    // -------------------------------------------------------------------------
    // Unknown slug → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_404_when_slug_does_not_exist()
    {
        var ct = TestContext.Current.CancellationToken;

        var user = MakeUser(5L, "user5@example.com", "Eve User");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(5L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/nonexistent-slug",
            new { name = "Whatever" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:not_found");
    }

    // -------------------------------------------------------------------------
    // Validation: empty name → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_name_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;

        var owner = MakeUser(6L, "owner6@example.com", "Frank Owner");
        var project = MakeProject(400L, "valid-project", "Valid Name", 6L);
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(6L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/valid-project",
            new { name = "", description = (string?)null },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");

        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("projects:project:name:required_or_too_long");
    }

    // -------------------------------------------------------------------------
    // Validation: description > 2000 chars → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_description_exceeds_2000_characters()
    {
        var ct = TestContext.Current.CancellationToken;

        var owner = MakeUser(7L, "owner7@example.com", "Grace Owner");
        var project = MakeProject(500L, "another-project", "Another Name", 7L);
        await fixture.Database.Save(owner, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(7L));

        var response = await client.PutAsJsonAsync(
            "/api/projects/another-project",
            new { name = "Still Valid", description = new string('x', 2001) },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");

        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("projects:project:description:too_long");
    }
}
