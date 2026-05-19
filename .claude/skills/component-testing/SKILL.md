---
name: component-testing
description: Use when writing tests for vertical slices — provides harness-based component testing patterns that test features end-to-end through HTTP endpoints with real dependencies via TestContainers
---

# Component Testing with Harness

## When to Use This Skill

Use this skill when:

- Writing tests for vertical slice features
- You need to test a feature as a whole (not individual classes)
- Working with external dependencies (DB, Redis, Kafka, file storage, etc.)
- Setting up integration tests that are easy to maintain during refactoring

**Announce at the start:** "I'm using the component-testing skill to write tests for this feature."

## Philosophy

**Component testing** means testing a vertical slice feature as a cohesive unit, including all its internal classes and logic, with external dependencies controlled through a harness.

**Benefits:**

- **Easy refactoring** — internal changes don't break tests
- **Realistic behavior** — tests run against real dependencies (PostgreSQL, Redis, Kafka)
- **Maintainability** — tests focus on behavior, not implementation details
- **Flexibility** — implementations can change without changing tests
- **Fast feedback** — TestContainers provide fast setup/teardown

**Not unit tests:** We don't test individual classes. We test the entire feature through its HTTP endpoint.

## TestContainers Preference

**IMPORTANT: Use TestContainers for real dependencies whenever possible.**

**Prefer (in priority order):**

1. **TestContainers with a real dependency** (PostgreSQL, Redis, Kafka containers)
2. **Lightweight alternatives** (SQLite instead of PostgreSQL, in-memory cache)
3. **Mocks** (only when TestContainers are impractical)

**Why TestContainers?**

- **Real behavior** — tests run against a real DB, not in-memory simulations
- **Integration issue detection** — SQL dialect differences, connection pools, etc.
- **Closer to production** — same DB engine as prod
- **Fast enough** — containers are reused within a collection
- **No surprises** — what works in tests works in production

**When to use alternatives:**

- **Performance constraints** — if TestContainers are too slow (rare with proper fixture setup)
- **CI constraints** — if the CI environment doesn't support Docker (rare)
- **External APIs** — use WireMock or mocks for third-party APIs

## What Is a Harness?

A **harness** is an abstraction of an external dependency that encapsulates:

- Starting the dependency (TestContainer, mock, etc.)
- Configuring the SUT to use it
- Setting up test data
- Verifying state after operations
- Cleanup between tests

### Harness Responsibilities

1. **Start** — launch a TestContainer or initialize a mock
2. **Configure** — override connection strings, register in DI
3. **Seed** — provide methods for setting up test data
4. **Assert** — provide methods for verifying results
5. **Stop** — clean up resources

## Base Interfaces

### IHarness Interface

```csharp
public interface IHarness<T> where T : class
{
    void ConfigureWebHostBuilder(IWebHostBuilder builder);
    Task Start(WebApplicationFactory<T> factory, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);
}
```

### Extension Method

```csharp
public static class HarnessExtensions
{
    public static WebApplicationFactory<T> AddHarness<T>(
        this WebApplicationFactory<T> factory,
        IHarness<T> harness)
        where T : class =>
        factory.WithWebHostBuilder(harness.ConfigureWebHostBuilder);
}
```

## Quick Start

### 1. Create Harness Implementations

**See [harnesses.md](harnesses.md) for full implementations:**

- DatabaseHarness with PostgreSQL TestContainer
- HttpClientHarness for HTTP requests
- Other harness types (Redis, Kafka, etc.)

### 2. Create TestFixture

**See [test-fixture.md](test-fixture.md) for the full implementation.**

Quick overview:

```csharp
public class TestFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestFixture()
    {
        Database = new DatabaseHarness<Program, AppDbContext>("DefaultConnection");
        HttpClient = new HttpClientHarness<Program>();

        _factory = new WebApplicationFactory<Program>()
            .AddHarness(Database)
            .AddHarness(HttpClient);
    }

    public DatabaseHarness<Program, AppDbContext> Database { get; }
    public HttpClientHarness<Program> HttpClient { get; }

    public async Task Reset(CancellationToken cancellationToken) =>
        await Database.Clear(cancellationToken);

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
}
```

### 3. Create xUnit Collection (domain-specific)

**IMPORTANT: Create domain-specific collections, NOT generic ones.**

```csharp
// GOOD — domain-specific collection
[CollectionDefinition(Name)]
public class SkillsTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(SkillsTestsCollection);
}

// GOOD — another domain-specific collection
[CollectionDefinition(Name)]
public class BlogTestsCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(BlogTestsCollection);
}

// BAD — generic collection name
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<TestFixture>
{
    public const string Name = nameof(DatabaseCollection);
}
```

**Collection naming rules:**

- Name collections by **domain / feature area** (Skills, Blog, Courses, Users, etc.)
- Use the pattern: `{Domain}TestsCollection` (e.g., SkillsTestsCollection, BlogTestsCollection)
- Do NOT use generic names like DatabaseCollection, ApiCollection, TestCollection
- Do NOT create one collection for all tests

**Why domain-specific collections?**

- **Parallel execution** — different domains can run in parallel (SkillsTests || BlogTests)
- **Isolation** — domain test data doesn't interfere with other collections
- **Clear organization** — tests are grouped by domain, matching vertical slice architecture
- **Performance** — TestContainers are reused within a domain, but domains run in parallel
- **Flexibility** — some domains may need different harness configurations

**Why collections are needed:**

- Reuse a single TestFixture (and TestContainers) across multiple test classes in a domain
- Control parallelism (tests in the same collection run sequentially, different collections run in parallel)
- Amortize the TestContainers startup cost within a domain

### 4. Writing Component Tests

```csharp
[Collection(UsersTestsCollection.Name)]
public class CreateAccountTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public CreateAccountTests(TestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_create_account()
    {
        // Arrange
        const string login = "Sam";
        var request = new CreateAccountRequestContract(login, "Qwer1234!");

        // Act
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        var response = await client.ExecutePostAsync<AccountContract>(
            "/auth/accounts", request, CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location().Should().Be($"/auth/accounts/{login.ToLower()}");
        response.Data.Login.Should().Be(login.ToLower());

        // Assert DB state (via harness)
        var dbAccount = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Login == login.ToLower(),
            CreateCancellationToken());

        dbAccount.Should().NotBeNull();
        dbAccount.PasswordHash.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_return_conflict_if_account_exists()
    {
        // Arrange
        await _fixture.Database.Save(CreateAccount("sam"));

        var request = new CreateAccountRequestContract("Sam", "Qwer1234!");

        // Act
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        var response = await client.ExecutePostAsync<ProblemDetailsContract>(
            "/auth/accounts", request, CreateCancellationToken());

        // Assert
        response.ShouldBeLogicConflictError(
            "Account already exists",
            "auth:logic:account_already_exists");
    }
}
```

**See [examples.md](examples.md) for more examples.**

## Component Test Patterns

### Test Structure (Arrange-Act-Assert)

1. **Arrange** — set up via harness methods
   ```csharp
   await _fixture.Database.Save(account, post);
   ```

2. **Act** — call the HTTP endpoint (tests the entire vertical slice)
   ```csharp
   var response = await client.ExecutePostAsync<Result>("/endpoint", request);
   ```

3. **Assert HTTP response** — status code, headers, body
   ```csharp
   response.StatusCode.Should().Be(HttpStatusCode.Created);
   response.Data.Should().NotBeNull();
   ```

4. **Assert side effects** — DB state, published messages, etc.
   ```csharp
   var entity = await _fixture.Database.SingleOrDefault<Entity>(x => x.Id == id);
   entity.Should().NotBeNull();
   ```

### What NOT to Test in Component Tests

**CRITICAL: NEVER test validation rules in component tests.**

**Do NOT create component tests for validation scenarios.**

Validation rules are tested in isolated unit tests using FluentValidation.TestHelper. Component tests focus on business logic, authorization, and side effects — NOT on validation.

**If a feature has a RequestValidator:**
1. Create a `ValidatorTests` class nested inside the component test file
2. Test ALL validation rules via FluentValidation.TestHelper
3. Component tests should NOT test validation errors (empty fields, max lengths, invalid formats, etc.)

**Example of what NOT to do:**

```csharp
// BAD — the validator is already tested in ValidatorTests
[Fact]
public async Task Should_return_validation_error_when_display_name_too_long()
{
    var request = new { DisplayName = new string('A', 101) };
    var response = await client.PatchAsJsonAsync("/api/users/me", request);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

**What to test in component tests:**
- Business logic and feature behavior
- Authorization (403 Forbidden, 401 Unauthorized)
- Resource existence (404 Not Found)
- Conflicts (409 Conflict)
- DB side effects
- Happy path scenarios with valid data

### Data Setup

```csharp
await _fixture.Database.Save(account);
await _fixture.Database.Save(account1, account2, post);
await _fixture.Database.Save(accountsList, postsList);

await _fixture.Database.Execute(async db =>
{
    db.Accounts.AddRange(accounts);
    await db.SaveChangesAsync();
});
```

### State Verification

```csharp
var account = await _fixture.Database.SingleOrDefault<Account>(
    x => x.Login == "sam",
    cancellationToken);

var count = await _fixture.Database.Execute(async db =>
    await db.Accounts.CountAsync());
```

### Testing with Authentication

```csharp
[Fact]
public async Task Should_require_authentication()
{
    var (client, account) = await _fixture.CreateAuthedHttpClient();

    var response = await client.GetAsync("/protected-resource");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

## Common Scenarios

### Testing Authorization

```csharp
[Fact]
public async Task Should_require_admin_role()
{
    var (client, account) = await _fixture.CreateAuthedHttpClient();

    var response = await client.DeleteAsync("/admin/users/123");

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### Testing Business Logic Errors

```csharp
[Fact]
public async Task Should_return_conflict_for_duplicate()
{
    await _fixture.Database.Save(CreatePost("my-slug"));

    var request = new CreatePostRequest("My Post", "my-slug", "Content");

    var response = await Act<ProblemDetailsContract>(request);

    response.ShouldBeLogicConflictError("Post with this slug already exists");
}
```

## Harness Development Guidelines

### Creating a New Harness

1. **Implement IHarness<T>**
2. **Use TestContainers** for a real dependency (preferred)
3. **ConfigureWebHostBuilder** — override connection strings or settings
4. **Start** — launch the TestContainer
5. **Stop** — clean up the TestContainer
6. **Add data setup methods** — for configuring test data
7. **Add verification methods** — for querying state to verify results
8. **Add a cleanup method** — fast reset between tests (e.g., Respawn for DB)

### Example: Redis Harness with TestContainers

```csharp
public class RedisHarness<TProgram> : IHarness<TProgram>
    where TProgram : class
{
    private RedisContainer? _redis;

    public void ConfigureWebHostBuilder(IWebHostBuilder builder)
    {
        builder.UseSetting("Redis:ConnectionString", _redis!.GetConnectionString());
    }

    public async Task Start(WebApplicationFactory<TProgram> factory, CancellationToken ct)
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync(ct);
    }

    public async Task Stop(CancellationToken ct)
    {
        if (_redis is not null)
        {
            await _redis.StopAsync(ct);
            await _redis.DisposeAsync();
        }
    }

    public async Task Set<T>(string key, T value) { /* ... */ }
    public async Task<T?> Get<T>(string key) { /* ... */ }
    public async Task Clear() { /* ... */ }
}
```

## Testing Workflow (TDD)

1. **Red** — write a failing component test
   - Define the HTTP request/response contract
   - Define the expected side effects (DB state, etc.)

2. **Green** — implement the vertical slice
   - Create Endpoint, Request, Validator, Handler
   - Run the test until it passes

3. **Refactor** — improve the implementation
   - Tests stay green (they test behavior, not implementation)

## Reference Documentation

- **[harnesses.md](harnesses.md)** — full harness implementations (DatabaseHarness, HttpClientHarness, etc.)
- **[test-fixture.md](test-fixture.md)** — full TestFixture implementation with helpers
- **[examples.md](examples.md)** — complete test examples for various scenarios

## Testing Checklist

When writing component tests:

- [ ] Create a harness for external dependencies (TestContainers preferred)
- [ ] Set up TestFixture with all harnesses
- [ ] Create an xUnit collection for fixture reuse
- [ ] Reset the fixture in `IAsyncLifetime.InitializeAsync()`
- [ ] Test through the HTTP endpoint (the entire vertical slice)
- [ ] Assert both the HTTP response and side effects
- [ ] Use harness data setup methods for arrange
- [ ] Use harness verification methods for assert
- [ ] Keep tests focused on feature behavior
- [ ] Add fixture helper methods for common scenarios

## Summary

**Component tests verify that vertical slice features work correctly as a whole:**
- Test through the HTTP endpoint (realistic)
- Use TestContainers for real dependencies (preferred)
- Use harnesses to abstract dependency setup
- Reset state between tests (fast with Respawn)
- Assert both the response and side effects

**Remember:** TestContainers provide the most realistic testing environment. Fall back to mocks only when truly necessary.
