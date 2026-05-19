# Vertical Slice Architecture — Implementation Patterns

Complete implementation patterns and examples for vertical slice architecture in DrimAgents.

## Complete Feature Templates

### Command Pattern (Create/Update/Delete)

```csharp
using System.Security.Claims;
using Api.Common;
using Api.Data;
using Api.Data.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Blog;

public static class CreatePost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapPost("/api/blog/posts", async Task<IResult> (
                [FromBody] Body body,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Results.Unauthorized();
                }

                var request = new Request(
                    Title: body.Title,
                    Slug: body.Slug,
                    Content: body.Content,
                    UserId: Guid.Parse(userId));

                var response = await sender.Send(request, cancellationToken);

                return Results.Created($"/api/blog/posts/{response.PostId}", response);
            })
            .RequireAuthorization("Admin")
            .WithTags("Blog")
            .WithOpenApi();
        }

        private record Body(string Title, string Slug, string Content);
    }

    public record Request(
        string Title,
        string Slug,
        string Content,
        Guid UserId) : IRequest<Response>;

    public record Response(Guid PostId);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(200).WithMessage("Title must be 200 characters or less");

            RuleFor(x => x.Slug)
                .NotEmpty()
                .MaximumLength(200)
                .Matches("^[a-z0-9-]+$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only");

            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Content is required");

            RuleFor(x => x.UserId)
                .NotEmpty();
        }
    }

    public class RequestHandler : IRequestHandler<Request, Response>
    {
        private readonly AppDbContext _db;
        private readonly ILogger<RequestHandler> _logger;

        public RequestHandler(AppDbContext db, ILogger<RequestHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var exists = await _db.Posts
                .AnyAsync(p => p.Slug == request.Slug.ToLower(), cancellationToken);

            if (exists)
            {
                throw new ValidationException("A post with this slug already exists");
            }

            var post = new Post
            {
                Id = Guid.NewGuid(),
                AuthorId = request.UserId,
                Title = request.Title,
                Slug = request.Slug.ToLower(),
                Content = request.Content,
                IsPublished = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created post {PostId} with slug {Slug}", post.Id, post.Slug);

            return new Response(post.Id);
        }
    }
}
```

### Query Pattern (Reading Data)

```csharp
using Api.Common;
using Api.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Blog;

public static class GetPost
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(WebApplication app)
        {
            app.MapGet("/api/blog/posts/{slug}", async Task<IResult> (
                string slug,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(slug);
                var response = await sender.Send(request, cancellationToken);

                return response != null
                    ? Results.Ok(response)
                    : Results.NotFound();
            })
            .WithTags("Blog")
            .WithOpenApi();
        }
    }

    public record Request(string Slug) : IRequest<Response?>;

    public record Response(
        Guid Id,
        string Title,
        string Slug,
        string Content,
        string AuthorName,
        DateTime PublishedAt);

    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Slug)
                .NotEmpty()
                .MaximumLength(200);
        }
    }

    public class RequestHandler : IRequestHandler<Request, Response?>
    {
        private readonly AppDbContext _db;

        public RequestHandler(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Response?> Handle(Request request, CancellationToken cancellationToken)
        {
            var post = await _db.Posts
                .Where(p => p.Slug == request.Slug.ToLower() && p.IsPublished)
                .Select(p => new Response(
                    p.Id,
                    p.Title,
                    p.Slug,
                    p.Content,
                    p.Author.DisplayName ?? p.Author.Email,
                    p.PublishedAt!.Value))
                .FirstOrDefaultAsync(cancellationToken);

            return post;
        }
    }
}
```

## Common Patterns

### Pagination

```csharp
public record Request(int Page = 1, int PageSize = 10) : IRequest<Response>;

public record Response(List<PostDto> Posts, int TotalCount, int Page, int PageSize);

public record PostDto(Guid Id, string Title, string Slug);

public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
{
    var query = _db.Posts.Where(p => p.IsPublished);

    var totalCount = await query.CountAsync(cancellationToken);

    var posts = await query
        .OrderByDescending(p => p.PublishedAt)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(p => new PostDto(p.Id, p.Title, p.Slug))
        .ToListAsync(cancellationToken);

    return new Response(posts, totalCount, request.Page, request.PageSize);
}
```

### Authorization Check in Handler

```csharp
public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
{
    var post = await _db.Posts.FindAsync(request.PostId);
    if (post == null) throw new NotFoundException("Post not found");

    if (post.AuthorId != request.UserId)
    {
        throw new UnauthorizedException("You can only edit your own posts");
    }

    // Continue with update...
}
```

### Custom Exceptions

Create in `backend/src/Api/Common/Exceptions.cs`:

```csharp
namespace Api.Common;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthorized") : base(message) { }
}
```

### Complex Validation (Cross-Field, Async)

```csharp
public class RequestValidator : AbstractValidator<Request>
{
    private readonly AppDbContext _db;

    public RequestValidator(AppDbContext db)
    {
        _db = db;

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x)
            .Must(x => x.StartDate < x.EndDate)
            .WithMessage("Start date must be before end date")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);

        RuleFor(x => x.Slug)
            .MustAsync(BeUniqueSlug)
            .WithMessage("Slug must be unique");
    }

    private async Task<bool> BeUniqueSlug(string slug, CancellationToken cancellationToken)
    {
        return !await _db.Posts.AnyAsync(p => p.Slug == slug.ToLower(), cancellationToken);
    }
}
```

### Returning Different Status Codes

```csharp
public class Endpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPut("/api/blog/posts/{id}", async Task<IResult> (
            Guid id,
            [FromBody] Body body,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var request = new Request(id, body.Title, body.Content, Guid.Parse(userId));
                await sender.Send(request, cancellationToken);
                return Results.NoContent();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedException ex)
            {
                return Results.Problem(ex.Message, statusCode: 403);
            }
        })
        .RequireAuthorization()
        .WithTags("Blog");
    }
}
```

## Anti-Patterns

### DON'T: Create separate files for each nested type

```csharp
// BAD: Split across multiple files
// CreatePostEndpoint.cs
// CreatePostRequest.cs
// CreatePostValidator.cs
// CreatePostHandler.cs
```

### DO: Keep everything in one file

```csharp
// GOOD: CreatePost.cs with all nested types
public static class CreatePost
{
    public class Endpoint : IEndpoint { }
    public record Request(...) : IRequest<Response>;
    public record Response(...);
    public class RequestValidator : AbstractValidator<Request> { }
    public class RequestHandler : IRequestHandler<Request, Response> { }
}
```

### DON'T: Make nested types public

```csharp
// BAD: Public nested types
public static class CreatePost
{
    public class Endpoint : IEndpoint { }  // public
    public class RequestValidator { }       // public
}
```

### DO: Leave nested types with default access level

```csharp
// GOOD: Internal/private nested types
public static class CreatePost
{
    class Endpoint : IEndpoint { }              // internal by default
    private record Body(...);                   // private, only used in endpoint
    public record Request(...) : IRequest<...>; // public (required for MediatR)
}
```

### DON'T: Bypass MediatR validation

```csharp
// BAD: Calling the handler directly
var handler = new CreatePost.RequestHandler(db, logger);
var response = await handler.Handle(request, cancellationToken);
```

### DO: Always use MediatR (validation runs automatically)

```csharp
// GOOD: MediatR pipeline includes validation
var response = await sender.Send(request, cancellationToken);
```

### DON'T: Put business logic in the endpoint

```csharp
// BAD: DB calls and logic in the endpoint
app.MapPost("/api/posts", async (Body body, AppDbContext db) =>
{
    var post = new Post { Title = body.Title };
    db.Posts.Add(post);
    await db.SaveChangesAsync();
    return Results.Created();
});
```

### DO: Keep endpoints thin — delegate to the handler

```csharp
// GOOD: Endpoint only maps HTTP, handler contains the logic
app.MapPost("/api/posts", async (Body body, ISender sender) =>
{
    var request = new Request(body.Title, body.Content);
    var response = await sender.Send(request, cancellationToken);
    return Results.Created($"/api/posts/{response.Id}", response);
});
```

### DON'T: Return entities directly

```csharp
// BAD: Exposing EF Core entities
public record Response(Post Post);  // Post is an EF entity

// Or worse:
return Results.Ok(post);  // Serializes the entire entity graph
```

### DO: Use DTO/Response records

```csharp
// GOOD: Explicit response shape
public record Response(Guid Id, string Title, string Slug, DateTime PublishedAt);

var response = new Response(post.Id, post.Title, post.Slug, post.PublishedAt!.Value);
return response;
```

### DON'T: Use magic strings for validation messages

```csharp
// BAD: Inconsistent messages
RuleFor(x => x.Title).NotEmpty().WithMessage("title required");
RuleFor(x => x.Slug).NotEmpty().WithMessage("The slug field is mandatory");
```

### DO: Use consistent, clear validation messages

```csharp
// GOOD: Clear, consistent messages
RuleFor(x => x.Title)
    .NotEmpty().WithMessage("Title is required")
    .MaximumLength(200).WithMessage("Title must be 200 characters or less");
```

## Testing Patterns

**For a complete testing guide, use the `component-testing` skill.**

The `component-testing` skill provides:

- **Harness-based testing** — abstracting external dependencies (DB, Redis, Kafka, etc.)
- **Component tests** — testing the entire feature through the HTTP endpoint, not internal classes
- **Flexible dependencies** — choose between real dependencies (TestContainers) or mocks
- **xUnit fixtures** — proper test lifecycle management and parallelism
- **Arrange/Act/Assert** — data setup, endpoint call, response and side-effect verification

**Quick example** (full details in the component-testing skill):

```csharp
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
        // Arrange
        var request = new CreateAccountRequestContract("Sam", "Qwer1234!");

        // Act
        var client = new RestClient(_fixture.HttpClient.CreateClient());
        var response = await client.ExecutePostAsync<AccountContract>(
            "/auth/accounts", request, CreateCancellationToken());

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Data.Login.Should().Be("sam");

        // Assert DB state via harness
        var dbAccount = await _fixture.Database.SingleOrDefault<Account>(
            x => x.Login == "sam", CreateCancellationToken());

        dbAccount.Should().NotBeNull();
        dbAccount.PasswordHash.Should().NotBeEmpty();
    }
}
```

**Why component tests?**

- **Test behavior, not implementation** — refactoring internals doesn't break tests
- **Realistic** — tests run the full request/response cycle
- **Maintainable** — changes to internal structure don't require rewriting tests
- **Flexible** — start with mocks, switch to real dependencies later (via harness)

**When to use unit tests:**

- Complex validation logic (FluentValidation rules)
- Complex algorithms or calculations
- Edge cases that are hard to trigger via HTTP

**Example test structure:**

```csharp
[Collection(BlogTestsCollection.Name)]
public class CreatePostTests : IAsyncLifetime
{
    private readonly TestFixture _fixture;

    public CreatePostTests(TestFixture fixture) => _fixture = fixture;
    public Task InitializeAsync() => _fixture.Reset(CreateCancellationToken());
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] public async Task Should_create_post() { }
    [Fact] public async Task Should_return_validation_error_for_invalid_slug() { }
    [Fact] public async Task Should_return_conflict_for_duplicate_slug() { }
    [Fact] public async Task Should_require_authentication() { }
    [Fact] public async Task Should_require_admin_role() { }
}
```

## Testing Checklist

For each feature, write component tests that verify:

### Component Tests (via HTTP endpoint)

- [ ] Happy path: valid request returns the expected response
- [ ] Response has the correct status code (200, 201, 204, etc.)
- [ ] Response body matches the expected shape
- [ ] Response headers are correct (Location, etc.)
- [ ] DB state is correct after the operation (via harness)
- [ ] Validation errors return 400 with problem details
- [ ] Not-found scenarios return 404
- [ ] Unauthenticated requests return 401
- [ ] Forbidden requests return 403
- [ ] Uniqueness constraints are handled correctly
- [ ] Edge cases: boundary values, empty collections, etc.

### Unit Tests (for complex logic)

- [ ] Complex validation rules in FluentValidation
- [ ] Complex algorithms or calculations
- [ ] Edge cases that are hard to trigger via HTTP
