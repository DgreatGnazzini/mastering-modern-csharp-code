#!/usr/bin/env dotnet

// An executable C# script. On Linux or macOS:
//   chmod +x P003_Shebang.cs
//   ./P003_Shebang.cs

using System.Runtime.InteropServices;

Console.WriteLine($"OS:      {RuntimeInformation.OSDescription}");
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Args:    {string.Join(", ", args)}");
