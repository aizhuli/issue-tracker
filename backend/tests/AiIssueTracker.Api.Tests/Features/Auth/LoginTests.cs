using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Features.Auth;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Auth;

[Collection(AuthTestsCollection.Name)]
public class LoginTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task SeedUser(string email, string password, string name)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password, name },
            ct);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Should_login_with_valid_credentials()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedUser("bob@example.com", "Sup3rSecret!", "Bob");

        using var client = fixture.HttpClient.CreateBffClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "Bob@Example.com", password = "Sup3rSecret!" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Login.Response>(ct);
        body!.Email.Should().Be("bob@example.com");
        body.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Should_return_unauthorized_for_wrong_password()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedUser("carol@example.com", "RightPassword1!", "Carol");

        using var client = fixture.HttpClient.CreateBffClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "carol@example.com", password = "WrongPassword!" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("auth:credentials:invalid");
    }

    [Fact]
    public async Task Should_return_unauthorized_for_unknown_email()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = fixture.HttpClient.CreateBffClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "ghost@example.com", password = "AnyPassword1!" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("auth:credentials:invalid");
    }

    public class ValidatorTests
    {
        private readonly Login.RequestValidator _validator = new();

        [Fact]
        public void Email_required()
        {
            var result = _validator.TestValidate(new Login.Request("", "Password1!"));
            result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("auth:user:email:required");
        }

        [Fact]
        public void Email_must_be_valid_format()
        {
            var result = _validator.TestValidate(new Login.Request("not-an-email", "Password1!"));
            result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("auth:user:email:invalid_format");
        }

        [Fact]
        public void Password_required()
        {
            var result = _validator.TestValidate(new Login.Request("a@b.co", ""));
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("auth:user:password:required");
        }

        [Fact]
        public void Valid_input_passes()
        {
            var result = _validator.TestValidate(new Login.Request("a@b.co", "Password1!"));
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
