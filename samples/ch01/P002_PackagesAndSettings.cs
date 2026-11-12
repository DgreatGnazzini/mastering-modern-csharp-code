#:package Humanizer@3.0.10
#:property InvariantGlobalization=true
#:property TreatWarningsAsErrors=true

// A file-based app with a NuGet package and build settings,
// and still no project file.
using Humanizer;

DateTime released = new(2025, 11, 11, 0, 0, 0, DateTimeKind.Utc);
DateTime now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

Console.WriteLine($".NET 10 shipped {(now - released).Humanize()} ago.");
Console.WriteLine("file_based_apps".Humanize(LetterCasing.Title));
Console.WriteLine($"{120.ToWords()} problems");
