using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Tests.Harnesses;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiIssueTracker.Api.Tests;

public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("issuetracker");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task ResetAsync(CancellationToken cancellationToken) =>
        await Database.Clear(cancellationToken);

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Database.Start(_factory, cts.Token);
        await HttpClient.Start(_factory, cts.Token);
        _ = _factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await HttpClient.Stop(cts.Token);
        await Database.Stop(cts.Token);
        await _factory.DisposeAsync();
    }
}
