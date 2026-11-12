using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace MasteringModernCSharp.Ch13;

public enum TicketCategory { Billing, Shipping, Technical, Other }

public record TicketTriage(
    TicketCategory Category,
    [property: Description("1 = urgent, 5 = whenever")] int Priority,
    string Summary);

public static class ChatSetup
{
    // Swap the provider here; nothing else in the app changes.
    public static IServiceCollection AddSupportChat(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddChatClient(_ =>
                new OpenAIClient(config["OpenAI:ApiKey"])
                    .GetChatClient(config["OpenAI:Model"] ?? "gpt-5-mini")
                    .AsIChatClient())
            .UseLogging()               // ILogger, from DI
            .UseOpenTelemetry();        // gen_ai.* spans and metrics
        return services;
    }
}

public class SupportTriage(IChatClient chat)
{
    private const string Instructions =
        "You triage customer support tickets. Be concise.";

    public async Task<TicketTriage> TriageAsync(
        string ticket, CancellationToken ct = default)
    {
        // Asks for JSON matching the record's schema and parses it.
        var response = await chat.GetResponseAsync<TicketTriage>(
            [
                new(ChatRole.System, Instructions),
                new(ChatRole.User, ticket),
            ],
            new ChatOptions { Temperature = 0 },
            cancellationToken: ct);
        return response.Result;
    }
}
