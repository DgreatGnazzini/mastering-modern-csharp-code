namespace MasteringModernCSharp.Ch05;

// HTTP-style headers: fast lookup by name, but sent in the order
// they were added.
public sealed class HeaderList
{
    private readonly OrderedDictionary<string, string> _headers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Set(string name, string value) => _headers[name] = value;

    public void InsertFirst(string name, string value)
    {
        _headers.Remove(name);
        _headers.Insert(0, name, value);
    }

    public string? Get(string name) => _headers.GetValueOrDefault(name);

    public KeyValuePair<string, string> this[int index] =>
        _headers.GetAt(index);

    public string Render() =>
        string.Join("\n", _headers.Select(h => $"{h.Key}: {h.Value}"));
}
