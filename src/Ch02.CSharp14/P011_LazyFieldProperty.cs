namespace MasteringModernCSharp.Ch02;

public class ReportTemplate(Func<IReadOnlyList<string>> loadLines)
{
    public int LoadCount { get; private set; }

    // Loaded on first read, then cached in the backing field.
    public IReadOnlyList<string> Lines => field ??= Load();

    private IReadOnlyList<string> Load()
    {
        LoadCount++;
        return loadLines();
    }
}
