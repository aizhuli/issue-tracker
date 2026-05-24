using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AiIssueTracker.Api.Common.OpenApi;

internal sealed class BffSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["BffSecret"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Bff-Secret",
            Description = "Shared BFF secret. Dev value: dev-bff-secret-change-me-please-32chars-min",
        };

        var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("BffSecret")] = [] };

        if (document.Paths is null) return Task.CompletedTask;

        foreach (var path in document.Paths.Values)
        {
            if (path.Operations is null) continue;
            foreach (var operation in path.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
