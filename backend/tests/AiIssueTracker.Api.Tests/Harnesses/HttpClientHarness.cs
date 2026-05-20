using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiIssueTracker.Api.Tests.Harnesses;

public class HttpClientHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    public const string SharedSecret = "test-bff-secret-32chars-minimum-aaaaaaa";
    public const string SecretHeader = "X-Bff-Secret";
    public const string UserIdHeader = "X-User-Id";

    private WebApplicationFactory<TProgram>? _factory;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("BffAuth:SharedSecret", SharedSecret);
    }

    public Task Start(WebApplicationFactory<TProgram> factory, CancellationToken cancellationToken)
    {
        _factory = factory;
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken) => Task.CompletedTask;

    public HttpClient CreateBffClient()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add(SecretHeader, SharedSecret);
        return client;
    }

    public HttpClient CreateUserClient(string encodedUserId)
    {
        var client = CreateBffClient();
        client.DefaultRequestHeaders.Add(UserIdHeader, encodedUserId);
        return client;
    }

    public HttpClient CreateRawClient() => _factory!.CreateClient();
}
