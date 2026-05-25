using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects;
using AwesomeAssertions;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class SlugAvailabilityTests(TestFixture fixture) : IAsyncLifetime
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

    private static Project MakeProject(long id, string slug, string name, long ownerId) =>
        new()
        {
            Id = id,
            Slug = slug,
            Name = name,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // Happy path: unused valid slug → available: true
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_available_true_for_unused_valid_slug()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1L, "user1@example.com", "User One");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1L));

        var response = await client.GetAsync("/api/projects/slug-availability?slug=my-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("my-project");
        body.Available.Should().BeTrue();
        body.Reason.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Existing slug → available: false, reason: "taken"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_available_false_with_reason_taken_when_slug_is_already_used()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(2L, "user2@example.com", "User Two");
        var project = MakeProject(100L, "taken-slug", "Taken Slug", 2L);
        await fixture.Database.Save(user, project);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(2L));

        var response = await client.GetAsync("/api/projects/slug-availability?slug=taken-slug", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("taken-slug");
        body.Available.Should().BeFalse();
        body.Reason.Should().Be("taken");
    }

    // -------------------------------------------------------------------------
    // Invalid format cases — all return 200 with available: false, reason: "invalid_format"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_invalid_format_when_slug_has_uppercase_letters()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(3L, "user3@example.com", "User Three");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(3L));

        var response = await client.GetAsync("/api/projects/slug-availability?slug=Web", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Available.Should().BeFalse();
        body.Reason.Should().Be("invalid_format");
    }

    [Fact]
    public async Task Should_return_invalid_format_when_slug_has_leading_hyphen()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(4L, "user4@example.com", "User Four");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(4L));

        var response = await client.GetAsync("/api/projects/slug-availability?slug=-foo", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Available.Should().BeFalse();
        body.Reason.Should().Be("invalid_format");
    }

    [Fact]
    public async Task Should_return_invalid_format_when_slug_has_trailing_hyphen()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(5L, "user5@example.com", "User Five");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(5L));

        var response = await client.GetAsync("/api/projects/slug-availability?slug=foo-", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Available.Should().BeFalse();
        body.Reason.Should().Be("invalid_format");
    }

    [Fact]
    public async Task Should_return_invalid_format_when_slug_is_too_short()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(6L, "user6@example.com", "User Six");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(6L));

        // 2-char slug: "ab" — the optional group needs {1,48} middle + 1 end char,
        // so "a" + "b" fails (the optional group requires 2+ chars); "ab" does not match.
        var response = await client.GetAsync("/api/projects/slug-availability?slug=ab", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SlugAvailability.Response>(ct);
        body.Should().NotBeNull();
        body!.Available.Should().BeFalse();
        body.Reason.Should().Be("invalid_format");
    }

    // -------------------------------------------------------------------------
    // Missing slug query param → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_slug_query_param_is_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(7L, "user7@example.com", "User Seven");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(7L));

        var response = await client.GetAsync("/api/projects/slug-availability", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Auth: missing X-User-Id → 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_403_when_user_id_header_is_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient(); // BFF secret present, no X-User-Id

        var response = await client.GetAsync("/api/projects/slug-availability?slug=my-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // Auth: missing BFF secret → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_401_when_bff_secret_is_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateRawClient(); // no headers at all

        var response = await client.GetAsync("/api/projects/slug-availability?slug=my-project", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
