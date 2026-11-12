namespace MasteringModernCSharp.Ch02;

public class Customer
{
    public DateTime? LastSeen { get; set; }
    public int Visits { get; set; }
    public List<string>? Tags { get; set; }
}

public static class VisitTracker
{
    public static void RecordVisit(Customer? customer, DateTime now)
    {
        // Each assignment runs only when customer is not null.
        customer?.LastSeen = now;
        customer?.Visits += 1;
        customer?.Tags?.Add("returning");
    }
}
