using System;
using System.Diagnostics.CodeAnalysis;

namespace SnapshotAssertions;

/// <summary>How line-ending differences between actual and expected content are handled
/// during snapshot comparison.</summary>
public enum SnapshotLineEndingMode
{
    /// <summary>No line-ending normalization: each line's ending is compared as it is, so CRLF and LF differ. Under
    /// <see cref="SnapshotTrailingNewline.Optional"/> the final line's ending, whichever it is, is not
    /// compared.</summary>
    Ordinal,

    /// <summary>Both sides are normalized to LF before comparison. Cross-platform-safe.</summary>
    NormalizeToLF,

    /// <summary>Both sides are normalized to CRLF before comparison. Windows-leaning.</summary>
    NormalizeToCRLF,

    /// <summary>Deprecated: use <see cref="NormalizeToLF"/>. For the comparison, line breaks are removed from both
    /// sides, so the text is compared as one run: blank lines and where lines break are not compared. A final newline
    /// is kept as one LF unless <see cref="SnapshotOptions.TrailingNewline"/> is
    /// <see cref="SnapshotTrailingNewline.Optional"/> (under the default <see cref="SnapshotTrailingNewline.Required"/>,
    /// <c>"a\nb\n"</c> compares as <c>"ab\n"</c>). The written actual file and an accepted baseline keep their line
    /// breaks, joined with LF, so a one-line baseline from an earlier version still matches.</summary>
    [SuppressMessage(
        "Info Code Smell",
        "S1133:Deprecated code should be removed",
        Justification = "Deliberate one-release deprecation window: the member is removed in 0.10.0, as its Obsolete message states.")]
    [Obsolete("IgnoreLineEndings also ignores where lines break and blank lines. Use NormalizeToLF; IgnoreLineEndings will be removed in 0.10.0.")]
    IgnoreLineEndings,
}
