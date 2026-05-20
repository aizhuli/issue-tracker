using System.Net;
using System.Net.Http.Json;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Features.Auth;
using AwesomeAssertions;
using FluentValidation.TestHelper;

namespace AiIssueTracker.Api.Tests.Features.Auth;

[Collection(AuthTestsCollection.Name)]
public class RegisterTests(TestFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(fixture.ResetAsync(TestContext.Current.CancellationToken));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Should_register_new_user_and_persist_password_hash()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "Alice@Example.com", password = "Sup3rSecret!", name = "Alice" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Register.Response>(ct);
        body.Should().NotBeNull();
        body!.Email.Should().Be("alice@example.com");
        body.Name.Should().Be("Alice");
        IdEncoding.TryDecode(body.Id, out _).Should().BeTrue();

        var user = await fixture.Database.SingleOrDefault<User>(u => u.Email == "alice@example.com", ct);
        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBeNullOrEmpty().And.NotBe("Sup3rSecret!");
    }

    [Fact]
    public async Task Should_return_conflict_when_email_already_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateBffClient();

        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "dup@example.com", password = "FirstPass1!", name = "First" },
            ct);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "DUP@example.com", password = "SecondPass2!", name = "Second" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<TestProblemDetails>(ct);
        problem!.ErrorCode.Should().Be("auth:user:email:already_exists");
    }

    [Fact]
    public async Task Should_reject_request_without_bff_secret()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.HttpClient.CreateRawClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "x@y.z", password = "Pass1234!", name = "X" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public class ValidatorTests
    {
        private readonly Register.RequestValidator _validator = new();

        [Fact]
        public void Email_required()
        {
            var result = _validator.TestValidate(new Register.Request("", "Password1!", "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("auth:user:email:required");
        }

        [Fact]
        public void Email_must_be_valid_format()
        {
            var result = _validator.TestValidate(new Register.Request("not-an-email", "Password1!", "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("auth:user:email:invalid_format");
        }

        [Fact]
        public void Email_max_length_254()
        {
            var local = new string('a', 250);
            var email = $"{local}@e.co";
            var result = _validator.TestValidate(new Register.Request(email, "Password1!", "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Email).WithErrorCode("auth:user:email:too_long");
        }

        [Fact]
        public void Password_required()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", "", "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("auth:user:password:required");
        }

        [Fact]
        public void Password_min_length_8()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", "short", "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("auth:user:password:too_short");
        }

        [Fact]
        public void Password_max_length_128()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", new string('p', 129), "Alice"));
            result.ShouldHaveValidationErrorFor(x => x.Password).WithErrorCode("auth:user:password:too_long");
        }

        [Fact]
        public void Name_required()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", "Password1!", ""));
            result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("auth:user:name:required");
        }

        [Fact]
        public void Name_max_length_100()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", "Password1!", new string('n', 101)));
            result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorCode("auth:user:name:too_long");
        }

        [Fact]
        public void Valid_input_passes()
        {
            var result = _validator.TestValidate(new Register.Request("a@b.co", "Password1!", "Alice"));
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
