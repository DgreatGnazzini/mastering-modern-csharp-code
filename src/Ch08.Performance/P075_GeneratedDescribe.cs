namespace MasteringModernCSharp.Ch08;

// The generator in Ch08.Generators writes a Describe() method
// for every partial type marked [AutoDescribe], at build time.
[AutoDescribe]
public partial class Invoice
{
    public string Number { get; init; } = "";
    public decimal Total { get; init; }
    public DateOnly Due { get; init; }
}
