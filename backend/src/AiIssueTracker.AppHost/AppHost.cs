var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithPgAdmin()
    .WithDataVolume();

var issueTrackerDb = postgres.AddDatabase("issuetracker");

var api = builder
    .AddProject<Projects.AiIssueTracker_Api>("api")
    .WithReference(issueTrackerDb)
    .WaitFor(issueTrackerDb);

builder
    .AddNpmApp("frontend", "../../../frontend", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
