namespace MasteringModernCSharp.Ch03.Tests;

public class P028_GenericsTests
{
    private static readonly Dog Rex = new(Guid.NewGuid(), "Rex", true);

    #region p028
    [Fact]
    public void Variance_lets_specific_and_general_types_meet()
    {
        var dogs = new InMemoryRepository<Dog>();
        dogs.Add(Rex);
        dogs.Add(new Dog(Guid.NewGuid(), "", GoodBoy: true));

        // out T: a repository of dogs IS a repository of animals.
        IReadRepository<Animal> animals = dogs;
        // in T: a validator of animals IS a validator of dogs.
        IValidator<Dog> dogCheck = new NameValidator();

        Assert.Equal(["Name is required."],
                     GenericHelpers.AuditDogs(animals, dogCheck));
    }
    #endregion

    [Fact]
    public void Constraint_allows_CompareTo_without_boxing()
    {
        Assert.Equal(9, GenericHelpers.Largest([3, 9, 4]));
        Assert.Equal("pear",
            GenericHelpers.Largest(["apple", "pear", "fig"]));
        Assert.Throws<InvalidOperationException>(
            () => GenericHelpers.Largest(Array.Empty<int>()));
    }

    [Fact]
    public void Repository_finds_by_id()
    {
        var cats = new InMemoryRepository<Cat>();
        var tom = new Cat(Guid.NewGuid(), "Tom");
        cats.Add(tom);
        Assert.Same(tom, cats.Find(tom.Id));
        Assert.Null(cats.Find(Guid.NewGuid()));
    }

    [Fact]
    public void GroupInto_groups_by_a_non_null_key()
    {
        var groups = GenericHelpers.GroupInto(
            new Animal[] { Rex, new Cat(Guid.NewGuid(), "Tom") },
            a => a.GetType().Name);
        Assert.Equal(["Dog", "Cat"], groups.Keys);
    }

    [Fact]
    public void Built_in_interfaces_are_variant_too()
    {
        IEnumerable<Animal> animals = new List<Dog> { Rex };
        IComparer<Dog> byName = Comparer<Animal>.Create(
            (a, b) => string.Compare(a.Name, b.Name,
                                     StringComparison.Ordinal));
        Assert.Single(animals);
        Assert.Equal(0, byName.Compare(Rex, Rex));
    }
}

public class P029_ReflectionTests
{
    private static readonly SignupRequest Request = new()
    {
        Email = "ada@example.com",
        Password = "hunter2",
        CardNumber = "4111111111111111",
        Age = 36,
    };

    #region p029
    [Fact]
    public void Sensitive_properties_are_masked()
    {
        var text = Redactor.ToLogString(Request);

        Assert.Equal(
            "SignupRequest { Email=ada@example.com, Password=***, " +
            "CardNumber=****-****, Age=36 }", text);
        Assert.DoesNotContain("hunter2", text);
    }
    #endregion

    [Fact]
    public void Metadata_is_read_once_per_type()
    {
        Redactor.ToLogString(Request);
        var before = Redactor.CachedTypes;
        for (var i = 0; i < 100; i++) Redactor.ToLogString(Request);
        Assert.Equal(before, Redactor.CachedTypes);
    }

    [Fact]
    public void Attribute_can_be_read_directly()
    {
        var attribute = typeof(SignupRequest)
            .GetProperty(nameof(SignupRequest.CardNumber))!
            .GetCustomAttributes(typeof(SensitiveAttribute), true)
            .Cast<SensitiveAttribute>()
            .Single();
        Assert.Equal("****-****", attribute.Mask);
    }

    [Fact]
    public void Nulls_print_as_null()
    {
        var text = Redactor.ToLogString(
            new SignupRequest { Email = "x", Password = "y" });
        Assert.Contains("CardNumber=****-****", text);   // still masked
        Assert.Contains("Age=0", text);
    }
}
