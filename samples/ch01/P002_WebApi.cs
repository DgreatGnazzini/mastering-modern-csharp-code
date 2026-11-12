#:sdk Microsoft.NET.Sdk.Web

// Swapping the SDK turns a script into a web app.
// Run: dotnet run P002_WebApi.cs
// Then open http://localhost:5000/hello/Ada
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!");

app.Run();
