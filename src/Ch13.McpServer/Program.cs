using MasteringModernCSharp.Ch13;

var builder = WebApplication.CreateBuilder(args);

// Any MCP client (VS Code, Claude, Copilot, your own app) can now
// discover and call the [McpServerTool] methods in OrderTools.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<OrderTools>();

var app = builder.Build();
app.MapMcp("/mcp");     // Streamable HTTP endpoint
app.Run();
