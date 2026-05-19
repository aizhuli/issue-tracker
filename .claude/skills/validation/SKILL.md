---
name: validation
description: Use when implementing form or input validation — provides full-stack validation patterns with FluentValidation (backend), Zod (frontend), and ProblemDetails error handling (project)
---

# Full-Stack Validation

All user input must be validated on both the frontend (UX) and the backend (security/source of truth).

## When to Use

Use this skill when:

- Creating forms that accept user input
- Implementing endpoints that receive data
- Adding file upload functionality
- Validating query parameters or route parameters
- Implementing business rule validation

## Validation Flow

```text
User fills out form
  |
Zod validates (client, instant feedback)
  |
Invalid -> Show errors immediately (no API call)
  |
Valid -> Send to BFF
  |
BFF passes to Backend (no validation)
  |
FluentValidation validates (server, authoritative)
  |
Invalid -> Return ProblemDetails (400)
  |
Valid -> Execute business logic
  |
Return success response
```

**Key principle:** Frontend validates for UX, backend validates for security. The backend is always the source of truth.

## Error Code Convention

Pattern: `domain:entity:field:error_type`

**Examples:**

- `blog:post:title:required`
- `blog:post:slug:invalid_format`
- `blog:post:slug:already_exists`
- `skills:skill:name:too_long`
- `users:email:invalid_format`
- `users:avatar:file_too_large`
- `courses:lesson:video:invalid_mime_type`

**Usage:**

- Backend returns error codes in ProblemDetails extensions
- Frontend maps error codes to i18n keys
- Falls back to English message if no translation exists

## Implementation Guides

See detailed patterns and examples in:

- **[Backend Validation](backend-validation.md)** — FluentValidation patterns, async validation, file uploads, ProblemDetails integration
- **[Frontend Validation](frontend-validation.md)** — Zod schemas, React Hook Form, error handling, i18n
- **[Common Patterns](validation-patterns.md)** — slug, email, URL, password, date validation at all levels
- **[Testing](validation-testing.md)** — validator unit tests with FluentValidation.TestHelper

## Quick Reference

### Backend (FluentValidation)

```csharp
public class RequestValidator : AbstractValidator<Request>
{
    public RequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .WithErrorCode("blog:post:title:required");

        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9-]+$")
            .WithErrorCode("blog:post:slug:invalid_format");
    }
}
```

### Frontend (Zod)

```typescript
const schema = z.object({
  title: z.string().min(1, 'Title is required'),
  slug: z.string().regex(/^[a-z0-9-]+$/, 'Invalid slug format')
});

const form = useForm({
  resolver: zodResolver(schema)
});
```

### BFF Layer

**Passthrough pattern** — no validation in BFF, ProblemDetails passed through unchanged.

```typescript
const response = await fetch(`${process.env.BACKEND_URL}/api/posts`, {
  method: 'POST',
  body: await request.text()
});

return new Response(await response.text(), {
  status: response.status
});
```

## Testing Validation Rules

**CRITICAL: EVERY feature with a RequestValidator MUST have validator unit tests.**

**If you created a RequestValidator without unit tests — that is a failure.**

Validation rules are tested in isolation using FluentValidation.TestHelper. **NEVER** test validation through component tests — create separate validator test classes.

**File organization:**
- Place the validator test class INSIDE the component test file as a nested class
- File location: `DrimAgents.Api.Tests/Features/{Domain}/{FeatureName}Tests.cs`
- Example: `CreateSkillTests.cs` contains the `CreateSkillTests` class with a nested `ValidatorTests`
- Add required using directives:
  - `using DrimAgents.Api.Features.{Domain};` (to access feature classes)
  - `using FluentValidation.TestHelper;` (for TestValidate() and assertion methods)

**Class naming convention:**
- Component tests: `{FeatureName}Tests`
- Validator tests (nested): `ValidatorTests` (nested inside component tests)

**See [validation-testing.md](validation-testing.md) for full patterns:**
- Overview of examples for testing all validation rules
- Use `TestValidate()` for synchronous validators
- Use `TestValidateAsync()` for async validators (DB checks)
- Test required fields, length limits, format patterns, and business rules
- Test both error cases AND successful cases

## Checklist

Before completing a validation implementation:

**Backend:**

- [ ] FluentValidation rules defined in `RequestValidator`
- [ ] Error codes follow the `domain:entity:field:error_type` convention
- [ ] Async validation uses `MustAsync` for DB checks
- [ ] ValidationBehavior registered in the MediatR pipeline
- [ ] ValidationExceptionHandler converts to ProblemDetails
- [ ] **Isolated validator unit tests created**

**Frontend:**

- [ ] Zod schemas mirror the backend FluentValidation rules
- [ ] React Hook Form uses `zodResolver` with the Zod schema
- [ ] Client-side validation provides instant feedback
- [ ] Server errors mapped from ProblemDetails to form fields
- [ ] Error codes mapped to i18n messages (if applicable)

**BFF:**

- [ ] API routes pass requests through unchanged
- [ ] API routes preserve status codes from the backend
- [ ] API routes return ProblemDetails unchanged
- [ ] No validation logic in the BFF layer
