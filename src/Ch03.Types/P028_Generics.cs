namespace MasteringModernCSharp.Ch03;

public interface IEntity
{
    Guid Id { get; }
}

public abstract record Animal(Guid Id, string Name) : IEntity;
public sealed record Dog(Guid Id, string Name, bool GoodBoy)
    : Animal(Id, Name);
public sealed record Cat(Guid Id, string Name) : Animal(Id, Name);

// "out T": this interface only hands T out, so a repository of
// dogs can be used where a repository of animals is expected.
public interface IReadRepository<out T> where T : IEntity
{
    T? Find(Guid id);
    IEnumerable<T> All();
}

// "in T": this interface only takes T in, so a validator for any
// animal can validate dogs.
public interface IValidator<in T>
{
    IEnumerable<string> Validate(T item);
}

// Constraints say what T must support, and the compiler checks it.
public sealed class InMemoryRepository<T> : IReadRepository<T>
    where T : class, IEntity
{
    private readonly Dictionary<Guid, T> _items = [];

    public void Add(T item) => _items[item.Id] = item;
    public T? Find(Guid id) => _items.GetValueOrDefault(id);
    public IEnumerable<T> All() => _items.Values;
}

public sealed class NameValidator : IValidator<Animal>
{
    public IEnumerable<string> Validate(Animal item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            yield return "Name is required.";
    }
}

public static class GenericHelpers
{
    // Works for any comparable type, with no boxing.
    public static T Largest<T>(IEnumerable<T> items)
        where T : IComparable<T>
    {
        using var e = items.GetEnumerator();
        if (!e.MoveNext())
            throw new InvalidOperationException("No items.");
        var best = e.Current;
        while (e.MoveNext())
        {
            if (e.Current.CompareTo(best) > 0) best = e.Current;
        }
        return best;
    }

    // notnull: keys can't be null, which Dictionary requires.
    public static Dictionary<TKey, List<TItem>> GroupInto<TKey, TItem>(
        IEnumerable<TItem> items, Func<TItem, TKey> key)
        where TKey : notnull
    {
        var groups = new Dictionary<TKey, List<TItem>>();
        foreach (var item in items)
        {
            if (!groups.TryGetValue(key(item), out var list))
                groups[key(item)] = list = [];
            list.Add(item);
        }
        return groups;
    }

    // Takes general animals and a dog-specific check.
    public static List<string> AuditDogs(
        IReadRepository<Animal> animals, IValidator<Dog> dogCheck) =>
        [.. animals.All().OfType<Dog>().SelectMany(dogCheck.Validate)];
}
