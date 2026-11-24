using System.Runtime.InteropServices;

namespace SnapshotAssertions;

/// <summary>A line of a <see cref="DocumentText"/>.</summary>
/// <param name="Start">The index of the line's first character in the text.</param>
/// <param name="Length">The length of the line's content, without its line break.</param>
/// <param name="BreakLength">The length of its line break: 2 for a carriage return and line feed, 1 for a line feed, 0
/// for a last line without one.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DocumentLine(int Start, int Length, int BreakLength);
