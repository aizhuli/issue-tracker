var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var issueTrackerDb = postgres.AddDatabase("issuetracker");

var bffSharedSecret = builder.AddParameter(
    "bff-shared-secret",
    "dev-bff-secret-change-me-please-32chars-min",
    secret: true);

var sessionCookiePassword = builder.AddParameter(
    "session-cookie-password",
    "dev-session-cookie-password-must-be-at-least-32-chars-long-for-iron-session",
    secret: true);

var llmBaseUrl = builder.AddParameter("llm-base-url", string.Empty);
var llmModel = builder.AddParameter("llm-model", string.Empty);
var llmApiKey = builder.AddParameter("llm-api-key", string.Empty, secret: true);

var api = builder
    .AddProject<Projects.AiIssueTracker_Api>("api")
    .WithReference(issueTrackerDb)
    .WaitFor(issueTrackerDb)
    .WithEnvironment("BffAuth__SharedSecret", bffSharedSecret)
    .WithEnvironment("Llm__BaseUrl", llmBaseUrl)
    .WithEnvironment("Llm__Model", llmModel)
    .WithEnvironment("Llm__ApiKey", llmApiKey);

builder
    .AddNpmApp("frontend", "../../../frontend", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithEnvironment("BFF_SHARED_SECRET", bffSharedSecret)
    .WithEnvironment("SESSION_COOKIE_PASSWORD", sessionCookiePassword)
    .WithEnvironment("API_URL", api.GetEndpoint("http"));

builder.Build().Run();
