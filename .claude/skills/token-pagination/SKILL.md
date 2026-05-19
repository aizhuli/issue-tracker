---
name: token-pagination
description: Use when implementing list endpoints that return paginated results — provides AIP-158-compliant token pagination with encrypted tokens and request validation (project)
---

# Token Pagination (AIP-158)

All list endpoints in DrimAgents MUST use token pagination following the Google AIP-158 standard.

## When to Use

Use this pattern for **every endpoint that returns a list of items**:

- Lists of skills, courses, blog posts
- User activity feeds
- Search results
- Admin tables
- Any endpoint with a collection

## Request Parameters

```csharp
app.MapGet("/posts", async Task<Ok<PageResponse<PostModel>>> (
    [FromQuery] string? pageToken,      // Opaque continuation token
    [FromQuery] int? maxPageSize,       // Max items per page
    [FromQuery] bool? published,        // Your filter parameters...
    ISender sender,
    CancellationToken cancellationToken) =>
{
    var response = await sender.Send(
        new Request(pageToken, maxPageSize, published),
        cancellationToken);
    return TypedResults.Ok(response);
});
```

**Parameters:**

- `pageToken` (string?, optional) — encrypted token from the previous response, null/empty for the first page
- `maxPageSize` (int?, optional) — max results per page, defaults to the configured value (typically 10-50)
- **All filter/search parameters** — must be included in the request for hash validation

## Response Structure

```csharp
public record PageResponse<T>(T[] Items, string? NextPageToken);
```

**Rules:**

- `Items` — array of results for the current page
- `NextPageToken` — null if the last page, otherwise an encrypted token for the next page

## Implementation Pattern

### 1. Handler Setup

```csharp
public class RequestHandler : IRequestHandler<Request, PageResponse<PostModel>>
{
    private readonly WebApiDbContext _db;
    private readonly LimitOffsetPaging _paging;

    public RequestHandler(WebApiDbContext db, LimitOffsetPaging paging)
    {
        _db = db;
        _paging = paging;
    }

    public async Task<PageResponse<PostModel>> Handle(Request request, CancellationToken ct)
    {
        // Step 1: Validate maxPageSize
        if (!_paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
        {
            throw PaginationExceptions.InvalidMaxPageSize();
        }

        // Step 2: Decode pageToken and validate request parameters
        if (!_paging.TryGetOffsetAndLimit(
            request.PageToken,
            maxPageSize,
            out var offset,
            out var limit,
            request.Published))  // CRITICAL: pass ALL filter parameters
        {
            throw PaginationExceptions.InvalidPageToken();
        }

        // Step 3: Build query with filters
        var query = _db.Posts.AsNoTracking();

        if (request.Published is not null)
        {
            query = query.Where(x => x.Published == request.Published.Value);
        }

        // Step 4: Execute query with pagination
        // CRITICAL: OrderBy must be on a stable field (usually Id)
        var items = await query
            .OrderBy(p => p.Id)
            .Skip(offset!.Value)
            .Take(limit!.Value)
            .Select(p => new PostModel(...))
            .ToArrayAsync(ct);

        // Step 5: Create next page token
        var nextPageToken = _paging.CreateNextPageToken(
            items.Length,
            offset.Value,
            limit.Value,
            request.Published);  // CRITICAL: pass the same filter parameters

        return new PageResponse<PostModel>(items, nextPageToken);
    }
}
```

## Critical Rules

### 1. Always pass ALL request parameters into hashing

The pagination system verifies that tokens are used with the same request parameters via a hash.

**Correct:**

```csharp
_paging.TryGetOffsetAndLimit(
    request.PageToken,
    maxPageSize,
    out var offset,
    out var limit,
    request.Published,
    request.Category,
    request.SearchQuery)

_paging.CreateNextPageToken(
    items.Length,
    offset.Value,
    limit.Value,
    request.Published,
    request.Category,
    request.SearchQuery)
```

**Wrong:**

```csharp
// Filter parameters omitted — token will be invalid when filters change
_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit)
```

**Why:** Prevents reuse of a page token with different filters, which would return incorrect results.

### 2. Always sort by a stable field

**Correct:**

```csharp
var items = await query
    .OrderBy(p => p.Id)              // Stable, unique sort
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

**Wrong:**

```csharp
// No sort — results will be unpredictable
var items = await query
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);

// Sort on a non-unique field — pagination will skip/duplicate items
var items = await query
    .OrderBy(p => p.Category)
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

**Why:** Without a stable sort, offset pagination returns inconsistent results when data changes.

### 3. Validate in the correct order

**Correct:**

```csharp
// 1. Validate maxPageSize first
if (!_paging.TryGetMaxPageSize(request.MaxPageSize, out var maxPageSize))
    throw PaginationExceptions.InvalidMaxPageSize();

// 2. Then validate and decode pageToken
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();
```

**Wrong:**

```csharp
// Validating pageToken before maxPageSize
if (!_paging.TryGetOffsetAndLimit(request.PageToken, request.MaxPageSize ?? 10, ...))
```

**Why:** maxPageSize validation must happen first to ensure a valid page size before decoding the token.

### 4. Return null NextToken on the last page

The `CreateNextPageToken` method automatically returns `null` when `count < limit`, indicating the last page.

```csharp
var nextPageToken = _paging.CreateNextPageToken(
    items.Length,      // If less than limit — returns null
    offset.Value,
    limit.Value,
    request.Published);

// nextPageToken will be null if items.Length < limit.Value
```

**Never set `nextPageToken = null` manually** — let the helper method handle this logic.

## Common Mistakes

### Mistake 1: Forgotten request parameters in token methods

```csharp
// WRONG — published filter missing from hash
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();

var query = _db.Posts.Where(x => x.Published == request.Published);

var nextPageToken = _paging.CreateNextPageToken(items.Length, offset.Value, limit.Value);
// Problem: user can reuse page 2 token with a different filter
```

**Fix:** Always pass all request parameters to both methods.

### Mistake 2: Sorting by a non-unique or unstable field

```csharp
// WRONG — Category is not unique
var items = await query
    .OrderBy(p => p.Category)
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
// Problem: multiple posts with the same category will have unpredictable order
```

**Fix:** Sort by a unique, stable field (usually `Id`). For custom sorting, use a composite sort:

```csharp
// CORRECT — custom sort with stable tiebreaker
var items = await query
    .OrderBy(p => p.Category)
    .ThenBy(p => p.Id)           // Stable tiebreaker
    .Skip(offset.Value)
    .Take(limit.Value)
    .ToArrayAsync(ct);
```

### Mistake 3: Unhandled null PageToken

```csharp
// WRONG — assumes pageToken is always present
var offset = DecodeToken(request.PageToken);  // Crashes on the first page
```

**Fix:** The `TryGetOffsetAndLimit` method handles null tokens automatically:

```csharp
// CORRECT
if (!_paging.TryGetOffsetAndLimit(request.PageToken, maxPageSize, out var offset, out var limit))
    throw PaginationExceptions.InvalidPageToken();
// If pageToken is null/empty — offset will be 0 (first page)
```

### Mistake 4: Different parameter order

```csharp
// WRONG — different parameter order in decode and create
_paging.TryGetOffsetAndLimit(
    request.PageToken, maxPageSize, out var offset, out var limit,
    request.Published, request.Category)

var nextPageToken = _paging.CreateNextPageToken(
    items.Length, offset.Value, limit.Value,
    request.Category, request.Published)  // Order swapped!
// Problem: the hash will differ, token validation will fail
```

**Fix:** Keep EXACTLY THE SAME parameter order for both calls.

## Configuration

Pagination behavior is configured in `appsettings.json`:

```json
{
  "Paging": {
    "TokenEncryptionKeyInBase64": "...",  // 32-byte AES key
    "TokenIvInBase64": "...",             // 16-byte IV
    "DefaultMaxPageSize": 10,             // Default if not specified
    "MaxMaxPageSize": 100                 // Upper limit
  }
}
```

**Rules:**

- The client can request any `maxPageSize` up to `MaxMaxPageSize`
- If the client exceeds the limit, the request is rejected with an `InvalidMaxPageSize` error
- If the client does not specify `maxPageSize`, `DefaultMaxPageSize` is used

## Error Handling

```csharp
// Invalid maxPageSize (negative, zero, or exceeds max)
throw PaginationExceptions.InvalidMaxPageSize();
// Returns: 400 Bad Request with error code "paging:validation:max_page_size_invalid"

// Invalid pageToken (corrupted, expired, wrong request parameters)
throw PaginationExceptions.InvalidPageToken();
// Returns: 400 Bad Request with error code "paging:validation:page_token_invalid"
```

## Security

**Token encryption:**

- Page tokens contain the offset and a hash of request parameters
- Encrypted with AES-256 using the configured key and IV
- Encoded with Crockford Base32 for URL safety
- Users cannot read or forge tokens

**Request validation:**

- The token includes a SHA-256 hash of all request parameters
- Prevents reuse of a token with different filters
- When filters change, the token is rejected

## Testing Pagination

When testing paginated endpoints:

```csharp
[Fact]
public async Task Should_paginate_posts()
{
    // Arrange
    var posts = Enumerable.Range(1, 25)
        .Select(i => CreatePost(name: $"Post {i}"))
        .ToArray();
    await _fixture.Database.Save(posts);

    var client = _fixture.CreateClient();

    // Act — first page
    var page1 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        "/posts?maxPageSize=10");

    // Assert — first page
    page1.ShouldNotBeNull();
    page1.Items.Should().HaveCount(10);
    page1.NextPageToken.Should().NotBeNullOrEmpty();

    // Act — second page
    var page2 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        $"/posts?maxPageSize=10&pageToken={page1.NextPageToken}");

    // Assert — second page
    page2.ShouldNotBeNull();
    page2.Items.Should().HaveCount(10);
    page2.NextPageToken.Should().NotBeNullOrEmpty();

    // Act — third page (last)
    var page3 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        $"/posts?maxPageSize=10&pageToken={page2.NextPageToken}");

    // Assert — last page
    page3.ShouldNotBeNull();
    page3.Items.Should().HaveCount(5);
    page3.NextPageToken.Should().BeNullOrEmpty();

    // Assert — no duplicates across pages
    var allIds = page1.Items
        .Concat(page2.Items)
        .Concat(page3.Items)
        .Select(p => p.Id)
        .ToArray();
    allIds.Should().OnlyHaveUniqueItems();
}

[Fact]
public async Task Should_reject_token_when_filters_change()
{
    // Arrange
    var client = _fixture.CreateClient();

    var page1 = await client.GetFromJsonAsync<PageResponse<PostModel>>(
        "/posts?published=true&maxPageSize=10");

    // Act — reuse token with different filter
    var response = await client.GetAsync(
        $"/posts?published=false&maxPageSize=10&pageToken={page1.NextPageToken}");

    // Assert — token rejected
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

## Checklist

Before completing a pagination implementation:

- [ ] Request has `pageToken` and `maxPageSize` parameters
- [ ] Response uses `PageResponse<T>` with `Items` and `NextPageToken`
- [ ] Handler validates `maxPageSize` first, then `pageToken`
- [ ] ALL request/filter parameters are passed to `TryGetOffsetAndLimit`
- [ ] The same parameters are passed to `CreateNextPageToken` in the same order
- [ ] Query uses `.OrderBy(x => x.Id)` or a stable composite sort
- [ ] Query uses `.Skip(offset.Value).Take(limit.Value)`
- [ ] Tests verify pagination across multiple pages
- [ ] Tests verify no duplicates across pages
- [ ] Tests verify `nextPageToken` is null on the last page
- [ ] Tests verify token rejection when filters change
