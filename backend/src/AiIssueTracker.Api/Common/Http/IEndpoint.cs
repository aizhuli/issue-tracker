using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiIssueTracker.Api.Common.Http;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpointTypes = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t));

        services.TryAddEnumerable(endpointTypes);
        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(app);
        }
        return app;
    }
}
