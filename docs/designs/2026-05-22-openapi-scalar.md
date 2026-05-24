# OpenAPI + Scalar Setup

**Date:** 2026-05-22  
**Status:** Approved

## Goal

Add interactive API documentation via Scalar, backed by .NET 10's built-in OpenAPI document generation. Available in all environments.

## Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| UI | Scalar | Already in package list (`Scalar.AspNetCore`) |
| Environments | All | Demo/teaching app — no reason to restrict |
| Security scheme | `X-Bff-Secret` API key (header) | Lets testers pre-fill the shared secret in Scalar UI |
| Spec path | `/openapi/v1.json` | .NET default |
| UI path | `/scalar/v1` | Scalar default |

## Changes

### 1. `Common/OpenApi/BffSecuritySchemeTransformer.cs` (new file)

Implements `IOpenApiDocumentTransformer`:

- Adds a `BffSecret` API key security scheme to `document.Components.SecuritySchemes` (header, name: `X-Bff-Secret`).
- Iterates all operations and appends a security requirement referencing `BffSecret`, so every endpoint inherits the padlock in Scalar.

### 2. `Program.cs` — service registration

```csharp
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BffSecuritySchemeTransformer>();
});
```

### 3. `Program.cs` — middleware pipeline (after `UseAuthorization`)

```csharp
app.MapOpenApi();
app.MapScalarApiReference();
```

## Out of Scope

- Per-endpoint response schema documentation (added automatically by .NET reflection).
- JWT / OAuth security schemes (not used in this project).
- Environment-gating the UI.
