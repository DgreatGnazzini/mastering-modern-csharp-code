using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasteringModernCSharp.Ch06;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EmailNotification), "email")]
[JsonDerivedType(typeof(SmsNotification), "sms")]
public abstract record Notification(string Recipient);

public record EmailNotification(string Recipient, string Subject)
    : Notification(Recipient);

public record SmsNotification(string Recipient, string Text)
    : Notification(Recipient);

public static class NotificationJson
{
    private static readonly JsonSerializerOptions Options =
        JsonSerializerOptions.Web;

    public static string Serialize(IEnumerable<Notification> items) =>
        JsonSerializer.Serialize(items, Options);

    public static List<Notification> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<Notification>>(json, Options)
            ?? [];
}
