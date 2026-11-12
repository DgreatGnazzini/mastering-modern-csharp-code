namespace MasteringModernCSharp.Ch04;

public static class Templates
{
    // `$$` means {{ }} marks a value, so single braces are plain JSON.
    public static string WebhookJson(string eventName, int orderId) =>
        $$"""
        {
          "event": "{{eventName}}",
          "data": { "orderId": {{orderId}} }
        }
        """;

    // SQL text only. Values go in as parameters, never in the string.
    public const string OrdersByCustomerSql = """
        SELECT o.Id, o.Total, o.CreatedAt
        FROM   Orders AS o
        WHERE  o.CustomerId = @customerId
          AND  o.Status <> 'Cancelled'
        ORDER  BY o.CreatedAt DESC
        """;
}
