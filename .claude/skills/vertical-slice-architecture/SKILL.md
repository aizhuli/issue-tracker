---
name: vertical-slice-architecture
description: Use when implementing backend features — provides vertical slice patterns with MediatR, FluentValidation, and IEndpoint (project)
---

# Implementing Vertical Slice Architecture

## When to Use This Skill

Use this skill when:

- Implementing ANY backend feature (command or query)
- Creating new vertical slices in `backend/src/Api/Features/`
- You need examples of validation, authorization, or error handling

**For testing vertical slices:** Use the `component-testing` skill. It provides harness-based testing patterns that test features as cohesive units through HTTP endpoints.

**Announce at the start:** "I'm using the vertical-slice-architecture skill to implement this feature."

## Project Structure

### Features (Vertical Slices)

Each feature = one static class file with nested types:

```text
Features/
├── [Domain]/
│   ├── [Feature].cs          # One file, all nested types
│   └── [Feature]Tests.cs     # Combined test (or in tests/ project)
```

**Example:** `Features/Blog/CreatePost.cs`

### Shared Infrastructure (Organized by Concern)

All shared infrastructure code is organized into folders by concern. **Each file belongs to a specific concern, not a generic category like "Middleware".**

```text
Common/
├── Auth/                     # Authentication and authorization
│   └── UserContextMiddleware.cs  # BFF header → Claims conversion
├── Exceptions/               # Exception handling and domain exceptions
│   ├── Exceptions.cs        # NotFoundException, ForbiddenException, etc.
│   └── ExceptionHandlerMiddleware.cs  # Global exception handling
├── Http/                     # HTTP/endpoint infrastructure
│   ├── IEndpoint.cs         # Interface for endpoint registration
│   └── HttpContextExtensions.cs  # Helpers for extracting user context
├── Identity/                 # ID generation utilities
│   ├── IdFactory.cs         # IdGen wrapper for DI
│   └── Base32Encoder.cs     # Crockford Base32 encoding/decoding
└── Validation/               # Validation infrastructure
    └── ValidationBehavior.cs  # MediatR pipeline behavior
```

**Namespace convention:** `DrimAgents.Api.Common.{Concern}`
- Example: `DrimAgents.Api.Common.Auth`, `DrimAgents.Api.Common.Exceptions`, `DrimAgents.Api.Common.Http`

**When creating new files in Common/:**
1. Identify the **specific concern** (Auth, Exceptions, Http, Identity, Validation, Caching, Storage, etc.)
2. Middleware belongs to the concern it serves (e.g., auth middleware → `Common/Auth/`, caching middleware → `Common/Caching/`)
3. Create a new concern folder as needed (e.g., `Common/Caching/`, `Common/Storage/`)
4. Place the file in the appropriate concern folder
5. Use the namespace `DrimAgents.Api.Common.{Concern}`

**Anti-pattern:** Do not create generic folders like `Middleware/`, `Utilities/`, `Helpers/` — always use a specific concern.

## Core Pattern

Each vertical slice follows the same structure:

1. **Endpoint** — maps the HTTP route, extracts the user, calls MediatR
2. **Request** — MediatR command/query with all input data
3. **Response** — explicit DTO returned by the handler
4. **RequestValidator** — FluentValidation rules
5. **RequestHandler** — business logic, database operations

## Quick Reference

### Minimal Command Example

```csharp
namespace Api.Features.Blog;

public static class CreatePost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapPost("/api/blog/posts", async (
                [FromBody] Body body,
                ISender sender,
                CancellationToken ct) =>
            {
                var request = new Request(body.Title, body.Slug);
                var response = await sender.Send(request, ct);
                return Results.Created($"/api/blog/posts/{response.PostId}", response);
            });
        }

        private record Body(string Title, string Slug);
    }

    public record Request(string Title, string Slug) : IRequest<Response>;
    public record Response(Guid PostId);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Slug).NotEmpty().Matches("^[a-z0-9-]+$");
        }
    }

    public class RequestHandler : IRequestHandler<Request, Response>
    {
        private readonly AppDbContext _db;

        public RequestHandler(AppDbContext db) => _db = db;

        public async Task<Response> Handle(Request request, CancellationToken ct)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Slug = request.Slug.ToLower()
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync(ct);

            return new Response(post.Id);
        }
    }
}
```

### Minimal Query Example

```csharp
namespace Api.Features.Blog;

public static class GetPost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapGet("/api/blog/posts/{slug}", async (
                string slug,
                ISender sender,
                CancellationToken ct) =>
            {
                var response = await sender.Send(new Request(slug), ct);
                return response != null ? Results.Ok(response) : Results.NotFound();
            });
        }
    }

    public record Request(string Slug) : IRequest<Response?>;

    public record Response(Guid Id, string Title, string Slug);

    public class RequestHandler : IRequestHandler<Request, Response?>
    {
        private readonly AppDbContext _db;

        public RequestHandler(AppDbContext db) => _db = db;

        public async Task<Response?> Handle(Request request, CancellationToken ct)
        {
            return await _db.Posts
                .Where(p => p.Slug == request.Slug && p.IsPublished)
                .Select(p => new Response(p.Id, p.Title, p.Slug))
                .FirstOrDefaultAsync(ct);
        }
    }
}
```

## Configuration Management

### Options Pattern

**Always use the Options pattern to access configuration** in vertical slices. Never inject `IConfiguration` directly into handlers.

**Structure:**
```text
Features/
└── [Domain]/
    ├── Options/
    │   └── [Domain]Options.cs    # Configuration class for the domain
    └── [Feature].cs               # Feature using IOptions<DomainOptions>
```

**Example:** `Features/Users/Options/UsersOptions.cs`

```csharp
namespace Api.Features.Users.Options;

public class UsersOptions
{
    public string[] AdminEmails { get; set; } = [];
}
```

**Usage in a handler:**

```csharp
using Microsoft.Extensions.Options;

public class RequestHandler : IRequestHandler<Request, Response>
{
    private readonly AppDbContext _db;
    private readonly UsersOptions _usersOptions;

    public RequestHandler(AppDbContext db, IOptions<UsersOptions> usersOptions)
    {
        _db = db;
        _usersOptions = usersOptions.Value;
    }

    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        var isAdmin = _usersOptions.AdminEmails.Contains(request.Email);
        // ...
    }
}
```

**Registration in Program.cs:**

```csharp
builder.Services.Configure<UsersOptions>(builder.Configuration.GetSection("Users"));
```

**Configuration in appsettings.json:**

```json
{
  "Users": {
    "AdminEmails": ["admin@example.com"]
  }
}
```

**Benefits:**
- Strongly typed configuration
- Testability (easy to mock options)
- Validation support (DataAnnotations or FluentValidation)
- Reloadable configuration (when using `IOptionsMonitor<T>`)

**Rules:**
- One options class per domain (e.g., `UsersOptions`, `CoursesOptions`)
- Place in `Features/[Domain]/Options/`
- Use `IOptions<T>` for static configuration
- Use `IOptionsMonitor<T>` for reloadable configuration
- Never inject `IConfiguration` directly into handlers

## Implementation Guide

See full patterns, examples, and anti-patterns in:

- **[Implementation Patterns](vsa-patterns.md)** — full templates, common patterns, authorization, pagination, testing, anti-patterns

## Feature Checklist

When implementing a new vertical slice:

- [ ] Create feature file: `Features/[Domain]/[Feature].cs`
- [ ] Implement nested `Endpoint` class with `MapEndpoint` method
- [ ] Define `Request` record implementing `IRequest<TResponse>`
- [ ] Define `Response` record (or `IRequest` for commands with no return)
- [ ] Create `RequestValidator` inheriting `AbstractValidator<Request>`
- [ ] Implement validation rules in the validator constructor
- [ ] Create `RequestHandler` implementing `IRequestHandler<Request, Response>`
- [ ] Inject dependencies in the handler constructor (`AppDbContext`, `ILogger`, etc.)
- [ ] Implement business logic in the `Handle` method
- [ ] Add authorization policy to the endpoint when needed (`.RequireAuthorization("PolicyName")`)
- [ ] Extract the user from `HttpContext.User` in the endpoint when needed
- [ ] Add structured logging in the handler (important operations, errors)
- [ ] Write component tests (see patterns in the `component-testing` skill)
- [ ] Test the happy path, validation errors, edge cases, and authorization
- [ ] Verify HTTP response and database state in tests
- [ ] Commit with a descriptive message following conventional commits

## Key Rules

1. **One feature = one file** — all nested types in one static class
2. **Nested types internal/private** — only `Request` and `Response` should be public
3. **Always use MediatR** — validation runs automatically through the pipeline
4. **Thin endpoints** — delegate all logic to the handler
5. **Use DTOs** — never return EF entities directly
6. **Component tests** — test through the HTTP endpoint, not internal classes

## Summary

Vertical Slice Architecture = One feature, one file

**Benefits:**

- Easy to find and modify features (everything in one place)
- Consistent structure for all features
- Automatic validation through the MediatR pipeline
- Testability in isolation with component tests

**Use this skill for every backend feature to maintain consistency and quality.**
