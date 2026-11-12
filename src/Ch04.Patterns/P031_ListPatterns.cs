namespace MasteringModernCSharp.Ch04;

public abstract record Command;
public sealed record AddTask(string Title) : Command;
public sealed record CompleteTask(int Id) : Command;
public sealed record ListTasks(bool IncludeDone) : Command;
public sealed record Help(string Reason) : Command;

public static class CommandParser
{
    public static Command Parse(string[] args) => args switch
    {
        ["add", .. var words] when words.Length > 0 =>
            new AddTask(string.Join(' ', words)),
        ["done", var id] when int.TryParse(id, out var n) =>
            new CompleteTask(n),
        ["list"] => new ListTasks(IncludeDone: false),
        ["list", "--all"] => new ListTasks(IncludeDone: true),
        [] => new Help("No command given."),
        [var unknown, ..] => new Help($"Unknown command '{unknown}'."),
    };
}
