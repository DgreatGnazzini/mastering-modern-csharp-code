namespace MasteringModernCSharp.Ch02;

public class Thermostat
{
    public const double AbsoluteZero = -273.15;

    // `field` is the compiler-generated backing field.
    public double TargetCelsius
    {
        get;
        set
        {
            ArgumentOutOfRangeException
                .ThrowIfLessThan(value, AbsoluteZero);
            field = value;
        }
    } = 20;

    public string Room
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Room is required.")
            : value.Trim();
    } = "Living room";
}
