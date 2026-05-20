using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Features.Auth;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace AiIssueTracker.Api.Tests.Features.Auth;

[Collection(AuthTestsCollection.Name)]
public class MeTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Register.Response> SeedUser(string email, string password, string name)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password, name },
            ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Register.Response>(ct))!;
    }

    [Fact]
    public async Task Should_return_current_user_when_authenticated()
    {
        var ct = TestContext.Current.CancellationToken;
        var registered = await SeedUser("dan@example.com", "Sup3rSecret!", "Dan");

        using var client = fixture.HttpClient.CreateUserClient(registered.Id);
        var response = await client.GetAsync("/api/auth/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Me.Response>(ct);
        body!.Id.Should().Be(registered.Id);
        body.Email.Should().Be("dan@example.com");
        body.Name.Should().Be("Dan");
        body.Avatar.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_forbidden_when_bff_secret_present_but_no_user_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient();

        var response = await client.GetAsync("/api/auth/me", ct);

        // 403 (not 401): the BFF authenticated successfully via the shared
        // secret, but the RequireUser policy needs a NameIdentifier claim
        // — ASP.NET returns Forbidden for an authenticated principal that
        // fails a policy.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Should_return_unauthorized_without_bff_secret()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateRawClient();

        var response = await client.GetAsync("/api/auth/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_return_not_found_for_stale_user_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var registered = await SeedUser("eve@example.com", "Sup3rSecret!", "Eve");

        await fixture.Database.Execute(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM users");
        });

        using var client = fixture.HttpClient.CreateUserClient(registered.Id);
        var response = await client.GetAsync("/api/auth/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("auth:user:not_found");
    }
}
