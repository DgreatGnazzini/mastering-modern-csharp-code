using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MasteringModernCSharp.Ch03;

// Marks data that must never reach a log.
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveAttribute : Attribute
{
    public string Mask { get; init; } = "***";
}

public sealed class SignupRequest
{
    public required string Email { get; init; }
    [Sensitive] public required string Password { get; init; }
    [Sensitive(Mask = "****-****")]
    public string? CardNumber { get; init; }
    public int Age { get; init; }
}

public static class Redactor
{
    private sealed record Field(
        string Name, Func<object, object?> Get, string? Mask);

    // Reflection is slow the first time. Read each type once.
    private static readonly ConcurrentDictionary<Type, Field[]> Cache =
        new();

    public static int CachedTypes => Cache.Count;

    public static string ToLogString<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value)
        where T : notnull
    {
        var fields = Cache.GetOrAdd(typeof(T), static t => Describe(t));
        var parts = fields.Select(f =>
            $"{f.Name}={f.Mask ?? f.Get(value)?.ToString() ?? "null"}");
        return $"{typeof(T).Name} {{ {string.Join(", ", parts)} }}";
    }

    private static Field[] Describe(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties)] Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken)   // declaration order
            .Select(p => new Field(
                p.Name,
                p.GetValue,
                p.GetCustomAttribute<SensitiveAttribute>()?.Mask))];
}
