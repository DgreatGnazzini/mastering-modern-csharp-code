namespace MasteringModernCSharp.Ch02;

// The part you write: declarations only.
public partial class Sensor
{
    public string Id { get; }

    public partial Sensor(string id);

    public partial event EventHandler<double>? Reading;
}
