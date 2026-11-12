// Problem 100: the whole system, described in C#.
// `dotnet run` here starts Redis in a container, the catalog
// service, and the Aspire dashboard with their logs and traces.
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithDataVolume();             // keep data between runs

var pricing = builder.AddContainer("pricing",
        "mockserver/mockserver", "5.15.0")
    .WithHttpEndpoint(targetPort: 1080, name: "http");

builder.AddProject<Projects.Ch11_Catalog>("catalog")
    .WithReference(cache)          // connection string "cache"
    .WithEnvironment("Services__Pricing",
        pricing.GetEndpoint("http"))
    .WaitFor(cache)                // start only once Redis is up
    .WithHttpHealthCheck("/health/ready")
    .WithReplicas(2);

builder.Build().Run();
