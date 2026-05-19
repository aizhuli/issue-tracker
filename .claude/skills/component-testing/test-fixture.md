# TestFixture Implementation Reference

This document contains the complete TestFixture implementation with helper methods. TestFixtures are created once per xUnit collection and reused across multiple test classes.

## Complete TestFixture Example

```csharp
using System.Net.Http.Headers;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YourApp.Database;
using YourApp.Domain;
using YourApp.Features.Auth.Services;
using YourApp.Tests.Harnesses;

namespace YourApp.Tests.Fixtures;

public class TestFixture : IAsyncLifetime
{
    static TestFixture()
    {
        SetupFluentAssertions();
    }

    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient);
    }

    public WebApplicationFactory<Program> Factory => _factory;
    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken)
    {
        await Database.Clear(cancellationToken);
    }

    public async Task<(HttpClient, Account)> CreateAuthedHttpClient()
    {
        var account = CreateAccount();
        await Database.Save(account);

        await using var scope = _factory.Services.CreateAsyncScope();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
        var jwt = jwtGenerator.Generate(account);

        var client = HttpClient.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);

        return (client, account);
    }

    public async Task<(HttpClient, Account)> CreateWronglyAuthedHttpClient()
    {
        var account = CreateAccount();
        await Database.Save(account);

        await using var scope = _factory.Services.CreateAsyncScope();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
        var incorrectJwt = jwtGenerator.Generate(account) + "123";

        var client = HttpClient.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", incorrectJwt);

        return (client, account);
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());

        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await Database.Stop(CreateCancellationToken());
    }

    // Workaround for FluentAssertions concurrency issue
    // https://github.com/fluentassertions/fluentassertions/issues/1932#issuecomment-1137366562
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void SetupFluentAssertions()
    {
        AssertionOptions.AssertEquivalencyUsing(options => options
            .Using<DateTimeOffset>(ctx => ctx.Subject.Should().BeSameDateAs(ctx.Expectation))
            .WhenTypeIs<DateTimeOffset>()
            .Using<DateTime>(ctx => ctx.Subject.Should().BeSameDateAs(ctx.Expectation))
            .WhenTypeIs<DateTime>()
        );
    }
}
```

## TestFixture with Multiple Databases

If the application uses multiple databases:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        AuthDb = new DatabaseHarness<Program, AuthDbContext>("AuthConnection");
        ContentDb = new DatabaseHarness<Program, ContentDbContext>("ContentConnection");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(AuthDb)
            .AddHarness(ContentDb)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AuthDbContext> AuthDb { get; }
    public DatabaseHarness<Program, ContentDbContext> ContentDb { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken)
    {
        await AuthDb.Clear(cancellationToken);
        await ContentDb.Clear(cancellationToken);
    }

    public async Task InitializeAsync()
    {
        await AuthDb.Start(_factory, CreateCancellationToken(60));
        await ContentDb.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await AuthDb.Stop(CreateCancellationToken());
        await ContentDb.Stop(CreateCancellationToken());
    }
}
```

## TestFixture with Additional Harnesses

Example with Redis and Kafka:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        Redis = new RedisHarness<Program>();
        Kafka = new KafkaHarness<Program>();
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(Redis)
            .AddHarness(Kafka)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public RedisHarness<Program> Redis { get; }
    public KafkaHarness<Program> Kafka { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken)
    {
        await Database.Clear(cancellationToken);
        await Redis.Clear();
    }

    public async Task InitializeAsync()
    {
        await Database.Start(_factory, CreateCancellationToken(60));
        await Redis.Start(_factory, CreateCancellationToken(30));
        await Kafka.Start(_factory, CreateCancellationToken(60));
        await HttpClient.Start(_factory, CreateCancellationToken());
        _ = _factory.Server;
    }

    public async Task DisposeAsync()
    {
        await HttpClient.Stop(CreateCancellationToken());
        await Database.Stop(CreateCancellationToken());
        await Redis.Stop(CreateCancellationToken());
        await Kafka.Stop(CreateCancellationToken());
    }
}
```

## Common Helper Methods

### Authentication Helpers

```csharp
public async Task<(HttpClient, Account)> CreateAuthedHttpClient()
{
    var account = CreateAccount();
    await Database.Save(account);

    var jwt = GenerateJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

public async Task<(HttpClient, Account)> CreateAdminHttpClient()
{
    var account = CreateAccount(role: "Admin");
    await Database.Save(account);

    var jwt = GenerateJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

public async Task<(HttpClient, Account)> CreateExpiredTokenHttpClient()
{
    var account = CreateAccount();
    await Database.Save(account);

    var jwt = GenerateExpiredJwt(account);

    var client = HttpClient.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    return (client, account);
}

private string GenerateJwt(Account account)
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var jwtGenerator = scope.ServiceProvider.GetRequiredService<JwtGenerator>();
    return jwtGenerator.Generate(account);
}
```

### Service Access Helpers

```csharp
public T GetService<T>() where T : notnull
{
    var scope = _factory.Services.CreateScope();
    return scope.ServiceProvider.GetRequiredService<T>();
}

public async Task WithService<T>(Func<T, Task> action) where T : notnull
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<T>();
    await action(service);
}
```

## xUnit Collection Setup

Each TestFixture needs a corresponding xUnit collection:

```csharp
using DrimAgents.Api.Tests.Fixtures;

namespace DrimAgents.Api.Tests.Features.Auth;

[CollectionDefinition(Name)]
public class AuthTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(AuthTestsCollection);
}
```

**Why collections?**
- **Fixture reuse** — one TestFixture instance is shared among all test classes in the collection
- **Parallelism control** — tests in the same collection run sequentially
- **Performance** — TestContainers start once, not per test class
- **Resource management** — containers are cleaned up once when the collection ends

## Using TestFixture in Tests

```csharp
using YourApp.Tests.Fixtures;

[Collection(AuthTestsCollection.Name)]
public class CreateAccountTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public CreateAccountTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_create_account()
    {
        var (client, account) = await _fixture.CreateAuthedHttpClient();
        // test logic...
    }
}
```

## Fixture Lifecycle

1. **Collection start** — TestFixture constructor runs once
2. **InitializeAsync** — starts all harnesses (starts TestContainers)
3. **Test class 1** — uses fixture
   - **IAsyncLifetime.InitializeAsync** — resets fixture state
   - **Test 1** — runs
   - **Test 2** — runs
   - **IAsyncLifetime.DisposeAsync** — no-op
4. **Test class 2** — uses the same fixture
   - **IAsyncLifetime.InitializeAsync** — resets fixture state
   - **Test 3** — runs
   - **IAsyncLifetime.DisposeAsync** — no-op
5. **Collection end** — TestFixture.DisposeAsync (stops containers)

## Best Practices

### DO: Use Reset() between tests
```csharp
public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
```
**Why:** Fast cleanup with Respawn (preserves schema, clears data)

### DO: Create helper methods for common scenarios
```csharp
public async Task<(HttpClient, Account)> CreateAuthedHttpClient() { }
public async Task<(HttpClient, Account)> CreateAdminHttpClient() { }
```
**Why:** Reduces duplication, makes tests more readable

### DO: Expose harnesses as properties
```csharp
public DatabaseHarness<Program, WebApiDbContext> Database { get; }
public HttpClientHarness<Program> HttpClient { get; }
```
**Why:** Clear, type-safe access to harness methods

### DON'T: Start/stop containers per test
```csharp
// BAD — too slow
public async Task InitializeAsync()
{
    await _fixture.Database.Start(...);
}
```
**Why:** Starting TestContainers is slow. Start once per collection, reset between tests.

### DON'T: Share mutable state between tests
```csharp
// BAD — state leaks between tests
private readonly Account _sharedAccount = CreateAccount();
```
**Why:** Tests must be independent. Use `Reset()` to clean up, create fresh entities in each test.

## Troubleshooting

### TestContainers are slow

**Solution:** Ensure containers are being reused:
- Use xUnit collections to reuse TestFixture
- Call `Reset()` between tests instead of restarting
- Use fast cleanup methods (Respawn for DB, FlushDatabase for Redis)

### Tests fail intermittently

**Solution:** Ensure proper cleanup:
- Call `Reset()` in `IAsyncLifetime.InitializeAsync()`
- Verify Respawn configuration includes all schemas
- Verify background tasks or async operations have completed

### Out of memory

**Solution:** Limit the number of parallel test collections:
- xUnit runs collections in parallel by default
- Set `maxParallelThreads` in xunit.runner.json if needed
- Ensure TestContainers are properly disposed

## Summary

**TestFixture responsibilities:**
1. Initialize harnesses once per collection
2. Expose harnesses for test access
3. Helper methods for common scenarios
4. Fast reset between tests
5. Cleanup when the collection ends

**Key patterns:**
- Use `IAsyncLifetime` for fixture lifecycle
- Create an xUnit collection for reuse
- Reset in the test's `IAsyncLifetime.InitializeAsync()`
- Expose harnesses as properties
- Add domain helper methods
