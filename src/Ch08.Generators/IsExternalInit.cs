// netstandard2.0 lacks this type, which records and init-only
// setters need. Declaring it ourselves is the standard fix.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
