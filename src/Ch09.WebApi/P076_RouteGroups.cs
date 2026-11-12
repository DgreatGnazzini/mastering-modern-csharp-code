using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MasteringModernCSharp.Ch09;

public record Todo(int Id, string Title, bool Done);
public record NewTodo(string Title);

public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _items = new();
    private int _nextId;

    public IEnumerable<Todo> All() => _items.Values.OrderBy(t => t.Id);
    public Todo? Find(int id) => _items.GetValueOrDefault(id);
    public Todo Add(string title)
    {
        var id = Interlocked.Increment(ref _nextId);
        var todo = new Todo(id, title, Done: false);
        _items[todo.Id] = todo;
        return todo;
    }
    public bool Remove(int id) => _items.TryRemove(id, out _);
}

public static class TodoEndpoints
{
    // Every todo route in one place, sharing a prefix and tag.
    public static RouteGroupBuilder MapTodos(
        this IEndpointRouteBuilder app)
    {
        var todos = app.MapGroup("/todos").WithTags("Todos");

        todos.MapGet("/", (TodoStore store) =>
            TypedResults.Ok(store.All()));
        todos.MapGet("/{id:int}", Get).WithName("GetTodo");
        todos.MapPost("/", Create);
        todos.MapDelete("/{id:int}", Delete);
        return todos;
    }

    // The return type lists every possible response, so the
    // compiler checks them and OpenAPI documents them.
    private static Results<Ok<Todo>, NotFound> Get(
        int id, TodoStore store) =>
        store.Find(id) is { } todo
            ? TypedResults.Ok(todo)
            : TypedResults.NotFound();

    private static CreatedAtRoute<Todo> Create(
        NewTodo input, TodoStore store)
    {
        var todo = store.Add(input.Title);
        return TypedResults.CreatedAtRoute(
            todo, "GetTodo", new { id = todo.Id });
    }

    private static Results<NoContent, NotFound> Delete(
        int id, TodoStore store) =>
        store.Remove(id)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
