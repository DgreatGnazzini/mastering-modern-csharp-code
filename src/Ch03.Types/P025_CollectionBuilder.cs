using System.Collections;
using System.Runtime.CompilerServices;

namespace MasteringModernCSharp.Ch03;

// Lets callers write: TagSet tags = ["urgent", "Billing", "urgent"];
[CollectionBuilder(typeof(TagSet), nameof(Create))]
public sealed class TagSet : IEnumerable<string>
{
    private readonly SortedSet<string> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    private TagSet() { }

    public int Count => _tags.Count;

    public bool Contains(string tag) => _tags.Contains(tag);

    // The compiler calls this with the elements of the expression.
    public static TagSet Create(ReadOnlySpan<string> tags)
    {
        var set = new TagSet();
        foreach (var tag in tags)
            set._tags.Add(tag.Trim());
        return set;
    }

    public IEnumerator<string> GetEnumerator() => _tags.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
