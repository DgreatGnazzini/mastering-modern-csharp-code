// Counts lines of code per file type under a folder.
// Run: dotnet run P001_LineCounter.cs -- ../../src

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
string[] extensions = [".cs", ".csproj", ".props", ".json", ".qmd"];

var totals = Directory
    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj"))
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin"))
    .Where(path => extensions.Contains(Path.GetExtension(path)))
    .GroupBy(Path.GetExtension)
    .Select(g => (Extension: g.Key, Files: g.Count(),
                  Lines: g.Sum(path => File.ReadLines(path).Count())))
    .OrderByDescending(t => t.Lines);

Console.WriteLine($"{"Type",-10}{"Files",8}{"Lines",10}");
foreach (var (extension, files, lines) in totals)
{
    Console.WriteLine($"{extension,-10}{files,8}{lines,10:N0}");
}
