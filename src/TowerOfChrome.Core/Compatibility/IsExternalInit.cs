// Polyfill required for C# 9 `record`/init-only-property support when targeting netstandard2.1
// (the real type ships in .NET 5+; the compiler only needs its presence, not its content).
// Standard, widely-used workaround for older TFMs — see dotnet/roslyn#45510.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
