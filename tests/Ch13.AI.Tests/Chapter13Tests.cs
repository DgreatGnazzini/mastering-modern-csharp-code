using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace MasteringModernCSharp.Ch13.Tests;

public class P118_ChatClientTests
{
    private static ChatMessage Reply(string text) =>
        new(ChatRole.Assistant, text);

    #region p118-test
    [Fact]
    public async Task Triage_parses_structured_output()
    {
        var fake = new FakeChatClient(Reply(
            """
            {"category":"Shipping","priority":2,
             "summary":"Parcel is a week late"}
            """));
        var triage = new SupportTriage(fake);

        var result = await triage.TriageAsync(
            "Where is my parcel? It's a week late!",
            TestContext.Current.CancellationToken);

        Assert.Equal(TicketCategory.Shipping, result.Category);
        Assert.Equal(2, result.Priority);
        // The request asked for JSON that matches TicketTriage.
        var format = Assert.IsType<ChatResponseFormatJson>(
            fake.Options[0]?.ResponseFormat);
        Assert.Contains("priority", format.Schema.ToString());
    }
    #endregion

    [Fact]
    public async Task System_prompt_comes_first()
    {
        var fake = new FakeChatClient(Reply(
            """{"category":"Other","priority":5,"summary":"Hi"}"""));

        await new SupportTriage(fake).TriageAsync(
            "Hello", TestContext.Current.CancellationToken);

        var sent = fake.Requests[0];
        Assert.Equal(ChatRole.System, sent[0].Role);
        Assert.Equal("Hello", sent[^1].Text);
        Assert.Equal(0f, fake.Options[0]?.Temperature);
    }

    [Fact]
    public async Task OpenTelemetry_middleware_emits_a_gen_ai_span()
    {
        var source = $"test-{Guid.NewGuid()}";
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);

        IChatClient chat = new FakeChatClient(Reply("pong"))
            .AsBuilder()
            .UseOpenTelemetry(sourceName: source)
            .Build();
        var response = await chat.GetResponseAsync(
            "ping",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("pong", response.Text);
        var span = Assert.Single(spans);
        Assert.Equal("chat", span.GetTagItem("gen_ai.operation.name"));
        Assert.Equal(
            "fake-model", span.GetTagItem("gen_ai.response.model"));
    }

    [Fact]
    public void Registers_the_pipeline_in_DI()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "sk-test",
            })
            .Build();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSupportChat(config)
            .BuildServiceProvider();

        var chat = services.GetRequiredService<IChatClient>();

        // Outermost middleware first, the OpenAI client at the core.
        Assert.NotNull(chat.GetService<LoggingChatClient>());
        Assert.NotNull(chat.GetService<OpenTelemetryChatClient>());
        Assert.Equal("gpt-5-mini",
            chat.GetService<ChatClientMetadata>()?.DefaultModelId);
    }
}

public class P119_FunctionCallingTests
{
    #region p119-test
    [Fact]
    public async Task Model_calls_our_method_then_answers()
    {
        var args = new Dictionary<string, object?>
        {
            ["orderId"] = "A-100",
        };
        var fake = new FakeChatClient(
            // 1st reply: the model asks us to run the tool.
            new(ChatRole.Assistant,
                [new FunctionCallContent(
                    "call-1", "get_order_status", args)]),
            // 2nd reply: the model has seen the result.
            new(ChatRole.Assistant, "It shipped and arrives on 6 Nov."));
        var assistant = new OrderAssistant(fake);

        var answer = await assistant.AskAsync(
            "Where is order A-100?",
            TestContext.Current.CancellationToken);

        Assert.Equal("It shipped and arrives on 6 Nov.", answer);
        // The second request carried our method's return value.
        var result = fake.Requests[1].SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Single();
        Assert.Equal("call-1", result.CallId);
        Assert.Contains("Shipped on 3 Nov", result.Result?.ToString());
    }
    #endregion

    [Fact]
    public void Tool_schema_comes_from_the_attributes()
    {
        var function = AIFunctionFactory.Create(
            OrderTools.GetOrderStatus, "get_order_status");

        Assert.Equal("Gets the shipping status of a customer order.",
                     function.Description);
        var schema = function.JsonSchema.GetRawText();
        Assert.Contains("\"orderId\"", schema);
        Assert.Contains("Order number, e.g. A-100", schema);
    }
}

public class P119_McpServerTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public P119_McpServerTests(WebApplicationFactory<Program> factory) =>
        _factory = factory;

    private Task<McpClient> ConnectAsync(CancellationToken ct) =>
        McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(
                        _factory.Server.BaseAddress, "/mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp,
                },
                _factory.CreateClient(), ownsHttpClient: true),
            cancellationToken: ct);

    #region p119-mcp-test
    [Fact]
    public async Task Client_discovers_and_calls_the_tool()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await ConnectAsync(ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        var tool = Assert.Single(tools);
        Assert.Equal("get_order_status", tool.Name);

        var result = await client.CallToolAsync("get_order_status",
            new Dictionary<string, object?> { ["orderId"] = "A-101" },
            cancellationToken: ct);
        var text = Assert.IsType<
            ModelContextProtocol.Protocol.TextContentBlock>(
                Assert.Single(result.Content));
        Assert.Equal("Waiting for stock", text.Text);
    }
    #endregion

    [Fact]
    public async Task Mcp_tools_plug_straight_into_IChatClient()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var client = await ConnectAsync(ct);
        IList<McpClientTool> tools =
            await client.ListToolsAsync(cancellationToken: ct);

        var args = new Dictionary<string, object?>
        {
            ["orderId"] = "A-100",
        };
        var fake = new FakeChatClient(
            new(ChatRole.Assistant,
                [new FunctionCallContent("c1", "get_order_status", args)]),
            new(ChatRole.Assistant, "Arriving 6 Nov."));
        IChatClient chat =
            fake.AsBuilder().UseFunctionInvocation().Build();

        // McpClientTool is an AIFunction: the call goes over MCP.
        var response = await chat.GetResponseAsync("Where is A-100?",
            new ChatOptions { Tools = [.. tools] }, ct);

        Assert.Equal("Arriving 6 Nov.", response.Text);
        var result = fake.Requests[1].SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Single();
        Assert.Contains("Shipped on 3 Nov",
            JsonSerializer.Serialize(result.Result));
    }
}

public class P120_SemanticSearchTests
{
    private static readonly DocChunk[] Docs =
    [
        new() { Id = "1", Title = "Returns",
                Text = "You can return any item within 30 days." },
        new() { Id = "2", Title = "Shipping",
                Text = "Orders ship within two days. Track your parcel." },
        new() { Id = "3", Title = "Payments",
                Text = "We accept cards and bank transfer." },
    ];

    #region p120-test
    [Fact]
    public async Task Finds_the_most_relevant_document_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var search = new DocSearch(
            new HashingEmbeddingGenerator(DocChunk.Dimensions));
        await search.IndexAsync(Docs, ct);

        var hits = await search.SearchAsync(
            "how do I track my parcel", top: 2, ct: ct);

        Assert.Equal(2, hits.Count);
        Assert.Equal("Shipping", hits[0].Title);
        Assert.True(hits[0].Score > hits[1].Score);
    }
    #endregion

    [Fact]
    public async Task Upsert_replaces_by_key()
    {
        var ct = TestContext.Current.CancellationToken;
        var search = new DocSearch(
            new HashingEmbeddingGenerator(DocChunk.Dimensions));
        await search.IndexAsync(Docs, ct);
        await search.IndexAsync(
            [new() { Id = "3", Title = "Payments",
                     Text = "We now accept crypto wallets." }], ct);

        var hits = await search.SearchAsync("crypto wallets", 3, ct);

        Assert.Equal(3, hits.Count);   // still three records
        Assert.Equal("Payments", hits[0].Title);
    }

    [Fact]
    public async Task Embeddings_are_unit_length_and_deterministic()
    {
        var generator = new HashingEmbeddingGenerator(64);
        var ct = TestContext.Current.CancellationToken;

        var a = await generator.GenerateVectorAsync(
            "hello world", cancellationToken: ct);
        var b = await generator.GenerateVectorAsync(
            "hello world", cancellationToken: ct);

        Assert.Equal(a.ToArray(), b.ToArray());
        var length = MathF.Sqrt(a.ToArray().Sum(x => x * x));
        Assert.Equal(1f, length, 0.0001f);
    }
}
