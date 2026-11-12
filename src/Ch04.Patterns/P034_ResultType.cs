namespace MasteringModernCSharp.Ch04;

public sealed record Error(string Code, string Message);

public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T? value, Error? error) =>
        (_value, _error) = (value, error);

    public bool IsSuccess => _error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => Fail(error);

    // Callers must handle both cases to get a value out.
    public TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> fail) =>
        _error is null ? ok(_value!) : fail(_error);

    public Result<TOut> Then<TOut>(Func<T, Result<TOut>> next) =>
        _error is null ? next(_value!) : _error;
}

public static class Signup
{
    public static Result<string> ValidateEmail(string email) =>
        email.Contains('@') && !email.StartsWith('@')
            ? email.Trim().ToLowerInvariant()
            : new Error("email.invalid", $"'{email}' is not an email.");

    public static Result<int> ValidateAge(int age) =>
        age is >= 13 and <= 120
            ? age
            : new Error("age.range", "Age must be between 13 and 120.");

    public static Result<string> Register(string email, int age) =>
        ValidateEmail(email).Then(valid =>
            ValidateAge(age).Then<string>(_ => $"Welcome, {valid}!"));
}
