# Mastering Modern C# — companion code

The complete, tested code for the book **Mastering Modern C#: 120 Real-World Solutions for Building Faster, Smarter Applications with C# 14 and .NET 10** by John Gnazzo.

Every listing in the book comes from this repository, and every problem from Chapter 2 on has tests.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see `global.json`)
- Docker, for the few tests that start a real database or container (Chapters 11 and 12). The other tests run without it.

## Build and test

```bash
dotnet build
dotnet test
```

Run the tests for one chapter, or for one problem:

```bash
dotnet test --project tests/Ch05.Collections.Tests
dotnet test --project tests/Ch05.Collections.Tests --filter-class "*P040*"
```

## How the code is organized

| Folder | What it holds |
| --- | --- |
| `samples/ch01/` | Chapter 1's file-based apps. Run one with `dotnet run P001_LineCounter.cs`. |
| `src/ChNN.Topic/` | One project per chapter. Each problem's solution is in a file named after it, such as `P045_NumericOrdering.cs`. |
| `tests/ChNN.Topic.Tests/` | The tests for that chapter, one test class per problem, such as `P045_NumericOrderingTests`. |

Shared settings (target framework, warnings as errors, package versions) are in `Directory.Build.props` and `Directory.Packages.props` at the root.

| Chapter | Projects |
| --- | --- |
| 1. Getting Started with .NET 10 and File-Based Apps | `samples/ch01` |
| 2. C# 14 Features in Practice | `Ch02.CSharp14` |
| 3. Types, Records and Immutability | `Ch03.Types` |
| 4. Pattern Matching and Functional Style | `Ch04.Patterns` |
| 5. Collections, LINQ and Data Processing | `Ch05.Collections` |
| 6. Text, JSON and Serialization | `Ch06.Text` |
| 7. Async, Concurrency and Parallelism | `Ch07.Async` |
| 8. Performance and Memory | `Ch08.Performance`, `Ch08.Benchmarks`, `Ch08.Generators` |
| 9. Web APIs with ASP.NET Core 10 | `Ch09.WebApi` |
| 10. Data Access with EF Core 10 | `Ch10.Data` |
| 11. Cloud-Native, Observability and Resilience | `Ch11.Catalog`, `Ch11.AppHost` |
| 12. Testing and Security | `Ch12.Security`, `Ch12.Orders` |
| 13. AI Integration with .NET | `Ch13.AI`, `Ch13.McpServer` |

## Using the code

The code is released under the [MIT License](LICENSE): use it in your own projects. The examples favor clarity over completeness. Security examples in particular are teaching examples: read the book's production notes and adapt them to your own threat model before using them in production.

## Reporting a problem

If something doesn't build, a test fails, or the code disagrees with the book, please [open an issue](../../issues) with the problem number, your operating system and the output of `dotnet --version`.
