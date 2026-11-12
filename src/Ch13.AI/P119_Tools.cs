using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace MasteringModernCSharp.Ch13;

// Plain C#. The attributes become the tool's description and
// JSON schema, which is all the model ever sees.
[McpServerToolType]
public sealed class OrderTools
{
    private static readonly Dictionary<string, string> Status = new()
    {
        ["A-100"] = "Shipped on 3 Nov, arriving 6 Nov",
        ["A-101"] = "Waiting for stock",
    };

    [McpServerTool(Name = "get_order_status", ReadOnly = true)]
    [Description("Gets the shipping status of a customer order.")]
    public static string GetOrderStatus(
        [Description("Order number, e.g. A-100")] string orderId) =>
        Status.TryGetValue(orderId, out var status)
            ? status : $"No order {orderId}.";
}

public class OrderAssistant(IChatClient inner)
{
    // UseFunctionInvocation runs the loop: model asks for a call,
    // the client runs it, sends the result back, model answers.
    private readonly IChatClient _chat = inner.AsBuilder()
        .UseFunctionInvocation()
        .Build();

    private readonly ChatOptions _options = new()
    {
        Tools = [AIFunctionFactory.Create(
            OrderTools.GetOrderStatus, "get_order_status")],
    };

    public async Task<string> AskAsync(
        string question, CancellationToken ct = default) =>
        (await _chat.GetResponseAsync(question, _options, ct)).Text;
}
