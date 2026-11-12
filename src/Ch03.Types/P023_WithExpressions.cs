namespace MasteringModernCSharp.Ch03;

public record Address(string Street, string City, string Country);

public record Contact(string Name, Address Address);

public record Shipment(
    string Id, Contact Recipient, IReadOnlyList<string> Items);

public static class ShipmentUpdates
{
    // Each level of nesting needs its own `with`.
    public static Shipment MoveTo(Shipment shipment, string newCity) =>
        shipment with
        {
            Recipient = shipment.Recipient with
            {
                Address = shipment.Recipient.Address with
                {
                    City = newCity,
                },
            },
        };

    // `with` copies the reference to the list, not the list itself,
    // so build a new list for the copy.
    public static Shipment AddItem(Shipment shipment, string item) =>
        shipment with { Items = [.. shipment.Items, item] };
}
