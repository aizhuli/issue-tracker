using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Auth;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Auth;

[Collection(AuthTestsCollection.Name)]
public class SearchUsersTests(TestFixture fixture) : IAsyncLifetime
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

    // -------------------------------------------------------------------------
    // Empty q → 200 with empty array
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchUsers_EmptyQ_Returns200EmptyArray()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        await fixture.Database.Save(caller);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsers_WhitespaceQ_Returns200EmptyArray()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        await fixture.Database.Save(caller);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search?q=   ", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body!.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Prefix match on name AND email; capped at 20
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchUsers_PrefixMatchOnName_ReturnsMatchingUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        var alice  = MakeUser(11L, "alice@example.com", "Alice");
        var alicia = MakeUser(12L, "alicia@example.com", "Alicia");
        var bob    = MakeUser(13L, "bob@example.com", "Bob");
        await fixture.Database.Save(caller, alice, alicia, bob);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search?q=Ali", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body!.Should().HaveCount(2);
        body.Select(u => u.Name).Should().Contain("Alice").And.Contain("Alicia");
        body.Select(u => u.Name).Should().NotContain("Bob");
    }

    [Fact]
    public async Task SearchUsers_PrefixMatchOnEmail_ReturnsMatchingUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller  = MakeUser(10L, "caller@example.com", "Caller");
        var support = MakeUser(11L, "support@acme.com", "Support");
        var sales   = MakeUser(12L, "sales@acme.com", "Sales");
        var hr      = MakeUser(13L, "hr@other.com", "HR");
        await fixture.Database.Save(caller, support, sales, hr);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search?q=support%40acme", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body!.Should().HaveCount(1);
        body[0].Name.Should().Be("Support");
    }

    [Fact]
    public async Task SearchUsers_ResultsCappedAt20()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        // Seed 25 users whose names all start with "Test"
        var users = Enumerable.Range(1, 25)
            .Select(i => MakeUser(100L + i, $"test{i:D2}@example.com", $"TestUser{i:D2}"))
            .ToArray();
        await fixture.Database.Save(caller);
        await fixture.Database.Save(users);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        // Pass maxPageSize=20 explicitly; the handler caps at min(maxPageSize, 20)
        var response = await client.GetAsync("/api/users/search?q=TestUser&maxPageSize=20", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body!.Should().HaveCount(20);
    }

    // -------------------------------------------------------------------------
    // Case-insensitive prefix
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchUsers_QueryIsCaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        var frank  = MakeUser(11L, "frank@example.com", "Frank");
        await fixture.Database.Save(caller, frank);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search?q=fRaNk", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchUsers.UserSearchResult[]>(ct);
        body!.Should().HaveCount(1);
        body[0].Name.Should().Be("Frank");
    }

    // -------------------------------------------------------------------------
    // maxPageSize > 20 → 400 validation error
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchUsers_MaxPageSizeOver20_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var caller = MakeUser(10L, "caller@example.com", "Caller");
        await fixture.Database.Save(caller);

        using var client = fixture.HttpClient.CreateUserClient(IdEncoding.Encode(10L));
        var response = await client.GetAsync("/api/users/search?q=any&maxPageSize=21", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("common:validation:failed");
    }

    // -------------------------------------------------------------------------
    // Nested validator unit tests (no HTTP)
    // -------------------------------------------------------------------------

    public class ValidatorTests
    {
        private readonly SearchUsers.RequestValidator _validator = new();

        [Fact]
        public void Q_null_passes()
        {
            var result = _validator.TestValidate(new SearchUsers.Request(null, null));
            result.ShouldNotHaveValidationErrorFor(x => x.Q);
        }

        [Fact]
        public void Q_100_chars_passes()
        {
            var result = _validator.TestValidate(new SearchUsers.Request(new string('a', 100), null));
            result.ShouldNotHaveValidationErrorFor(x => x.Q);
        }

        [Fact]
        public void Q_101_chars_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(new SearchUsers.Request(new string('a', 101), null));
            result.ShouldHaveValidationErrorFor(x => x.Q)
                .WithErrorCode("auth:users:search:q_too_long");
        }

        [Fact]
        public void MaxPageSize_null_passes()
        {
            var result = _validator.TestValidate(new SearchUsers.Request("alice", null));
            result.ShouldNotHaveValidationErrorFor(x => x.MaxPageSize);
        }

        [Fact]
        public void MaxPageSize_20_passes()
        {
            var result = _validator.TestValidate(new SearchUsers.Request("alice", 20));
            result.ShouldNotHaveValidationErrorFor(x => x.MaxPageSize);
        }

        [Fact]
        public void MaxPageSize_21_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(new SearchUsers.Request("alice", 21));
            result.ShouldHaveValidationErrorFor(x => x.MaxPageSize)
                .WithErrorCode("auth:users:search:max_page_size_invalid");
        }

        [Fact]
        public void MaxPageSize_0_fails_with_correct_error_code()
        {
            var result = _validator.TestValidate(new SearchUsers.Request("alice", 0));
            result.ShouldHaveValidationErrorFor(x => x.MaxPageSize)
                .WithErrorCode("auth:users:search:max_page_size_invalid");
        }
    }
}
