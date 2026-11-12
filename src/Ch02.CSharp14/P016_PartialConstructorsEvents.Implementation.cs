namespace MasteringModernCSharp.Ch02;

// The part a source generator would emit: the implementations.
public partial class Sensor
{
    private EventHandler<double>? _reading;

    public int SubscriberCount =>
        _reading?.GetInvocationList().Length ?? 0;

    public partial Sensor(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public partial event EventHandler<double>? Reading
    {
        add => _reading += value;
        remove => _reading -= value;
    }

    public void Publish(double value) => _reading?.Invoke(this, value);
}
