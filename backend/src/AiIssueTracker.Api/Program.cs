using System.Reflection;
using System.Security.Claims;
using AiIssueTracker.Api.Common.Auth;
using AiIssueTracker.Api.Common.Exceptions;
using AiIssueTracker.Api.Common.Http;
using AiIssueTracker.Api.Common.Identity;
using AiIssueTracker.Api.Common.OpenApi;
using AiIssueTracker.Api.Common.Pagination;
using AiIssueTracker.Api.Common.Validation;
using AiIssueTracker.Api.Data;
using AiIssueTracker.Api.Data.Entities;
using AiIssueTracker.Api.Integrations.Llm;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("issuetracker");

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.AddLlmChatClient();

builder.Services.AddIdFactory(builder.Configuration.GetValue("IdGenerator:GeneratorId", 0));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.Configure<BffAuthOptions>(builder.Configuration.GetSection(BffAuthOptions.SectionName));

builder.Services
    .AddAuthentication(BffAuthenticationHandler.SchemeName)
    .AddScheme<BffAuthenticationOptions, BffAuthenticationHandler>(BffAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.BffOnly, policy => policy
        .AddAuthenticationSchemes(BffAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser());

    options.AddPolicy(AuthPolicies.RequireUser, policy => policy
        .AddAuthenticationSchemes(BffAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.NameIdentifier));
});

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHashing, PasswordHashing>();

builder.Services.Configure<PagingOptions>(builder.Configuration.GetSection(PagingOptions.SectionName));
builder.Services.AddSingleton<LimitOffsetPaging>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BffSecuritySchemeTransformer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithProxy(string.Empty);  // keep auth headers local — don't route through proxy.scalar.com

    if (app.Environment.IsDevelopment())
    {
        options.AddApiKeyAuthentication("BffSecret", scheme =>
        {
            scheme.Value = "dev-bff-secret-change-me-please-32chars-min";
        });
    }
});

app.MapDefaultEndpoints();
app.MapEndpoints();

app.Run();

public partial class Program;
