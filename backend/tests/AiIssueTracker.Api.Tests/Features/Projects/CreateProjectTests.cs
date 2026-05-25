using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Projects;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Projects;

[Collection(ProjectsTestsCollection.Name)]
public class CreateProjectTests(TestFixture fixture) : IAsyncLifetime
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
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_create_project_and_return_201_with_location_header()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(1L);
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(1L));

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "my-project", name = "My Project", description = "A test project" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/api/projects/my-project");

        var body = await response.Content.ReadFromJsonAsync<CreateProject.Response>(ct);
        body.Should().NotBeNull();
        body!.Slug.Should().Be("my-project");
        body.Name.Should().Be("My Project");
        body.Description.Should().Be("A test project");
        body.OwnerId.Should().Be(IdEncoding.Encode(1L));
        body.OwnerName.Should().Be("Owner");
        IdEncoding.TryDecode(body.Id, out _).Should().BeTrue();
        body.CreatedAt.Should().Be(body.UpdatedAt);
    }

    [Fact]
    public async Task Should_lowercase_slug_and_persist_correct_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(2L, "owner2@example.com", "Owner Two");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(2L));

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "web-platform", name = "Web Platform" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var project = await fixture.Database.SingleOrDefault<Project>(p => p.Slug == "web-platform", ct);
        project.Should().NotBeNull();
        project!.Slug.Should().Be("web-platform");
        project.OwnerId.Should().Be(2L);
        project.CreatedAt.Should().Be(project.UpdatedAt);
    }

    // -------------------------------------------------------------------------
    // Slug collision → 409
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_409_when_slug_already_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(3L, "owner3@example.com", "Owner Three");
        var existing = new Project
        {
            Id = 100L,
            Slug = "web",
            Name = "Web",
            OwnerId = 3L,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await fixture.Database.Save(user, existing);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(3L));

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "web", name = "Other" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("projects:project:slug:already_exists");
    }

    // -------------------------------------------------------------------------
    // Invalid slug format → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_slug_has_uppercase_letters()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(4L, "owner4@example.com", "Owner Four");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(4L));

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "Web-Platform", name = "X" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("projects:project:slug:invalid_format");
    }

    // -------------------------------------------------------------------------
    // Name length boundaries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_400_when_name_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(5L, "owner5@example.com", "Owner Five");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(5L));

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "valid-slug", name = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("projects:project:name:required_or_too_long");
    }

    [Fact]
    public async Task Should_return_201_when_name_is_exactly_100_characters()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(6L, "owner6@example.com", "Owner Six");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(6L));

        var name100 = new string('a', 100);
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "slug-100", name = name100 },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_return_400_when_name_is_101_characters()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = MakeUser(7L, "owner7@example.com", "Owner Seven");
        await fixture.Database.Save(user);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(7L));

        var name101 = new string('a', 101);
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "slug-101", name = name101 },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        json!.RootElement.GetProperty("errorCode").GetString().Should().Be("common:validation:failed");
        var codes = ExtractValidationErrorCodes(json);
        codes.Should().Contain("projects:project:name:required_or_too_long");
    }

    // -------------------------------------------------------------------------
    // Auth: missing X-User-Id → 403 Forbidden
    //
    // The BFF shared secret authenticates the caller but the RequireUser policy
    // also needs a NameIdentifier claim. When X-User-Id is absent the principal
    // is authenticated (secret is valid) but not authorized → 403, not 401.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Should_return_403_when_user_id_header_is_missing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient(); // BFF secret present, no X-User-Id

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "no-user", name = "No User" },
            ct);

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

        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new { slug = "no-secret", name = "No Secret" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly CreateProject.RequestValidator _validator = new();

        [Fact]
        public void Name_required()
        {
            var result = _validator.TestValidate(new CreateProject.Request("", "valid-slug", null));
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorCode("projects:project:name:required_or_too_long");
        }

        [Fact]
        public void Name_max_length_100()
        {
            var result = _validator.TestValidate(
                new CreateProject.Request(new string('x', 101), "valid-slug", null));
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorCode("projects:project:name:required_or_too_long");
        }

        [Fact]
        public void Name_exactly_100_chars_passes()
        {
            var result = _validator.TestValidate(
                new CreateProject.Request(new string('x', 100), "valid-slug", null));
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Slug_required()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "", null));
            result.ShouldHaveValidationErrorFor(x => x.Slug)
                .WithErrorCode("projects:project:slug:invalid_format");
        }

        [Fact]
        public void Slug_must_be_lowercase()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "Web-Platform", null));
            result.ShouldHaveValidationErrorFor(x => x.Slug)
                .WithErrorCode("projects:project:slug:invalid_format");
        }

        [Fact]
        public void Slug_cannot_have_leading_hyphen()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "-bad-slug", null));
            result.ShouldHaveValidationErrorFor(x => x.Slug)
                .WithErrorCode("projects:project:slug:invalid_format");
        }

        [Fact]
        public void Slug_cannot_have_trailing_hyphen()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "bad-slug-", null));
            result.ShouldHaveValidationErrorFor(x => x.Slug)
                .WithErrorCode("projects:project:slug:invalid_format");
        }

        [Fact]
        public void Slug_valid_lowercase_alphanumeric_passes()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "my-project", null));
            result.ShouldNotHaveValidationErrorFor(x => x.Slug);
        }

        [Fact]
        public void Description_null_passes()
        {
            var result = _validator.TestValidate(new CreateProject.Request("My Project", "my-project", null));
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Description_max_length_2000()
        {
            var result = _validator.TestValidate(
                new CreateProject.Request("My Project", "my-project", new string('d', 2001)));
            result.ShouldHaveValidationErrorFor(x => x.Description)
                .WithErrorCode("projects:project:description:too_long");
        }

        [Fact]
        public void Description_exactly_2000_chars_passes()
        {
            var result = _validator.TestValidate(
                new CreateProject.Request("My Project", "my-project", new string('d', 2000)));
            result.ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Valid_full_input_passes()
        {
            var result = _validator.TestValidate(
                new CreateProject.Request("My Project", "my-project", "A description"));
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
