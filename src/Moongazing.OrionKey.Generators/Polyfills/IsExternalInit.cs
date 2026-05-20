// Polyfill enabling C# 9 'init' accessors and positional records on netstandard2.0.
namespace System.Runtime.CompilerServices;

using System.ComponentModel;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
