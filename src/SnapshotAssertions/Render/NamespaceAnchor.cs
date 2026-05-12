namespace SnapshotAssertions.Render;

/// <summary>
/// Internal marker that anchors the <c>SnapshotAssertions.Render</c> namespace as the
/// family convention for text renderer entry points. Sibling family packages publish
/// their renderer types under this namespace in their own assemblies so consumers discover
/// them with a single <c>using SnapshotAssertions.Render;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The convention is namespace-shared, not type-shared: types co-exist by sharing the
/// namespace name across assemblies. Cross-assembly partial classes do not compose, so this
/// package deliberately does not publish a "renderer hub" static class for sibling
/// packages to extend. Each package owns its own renderer types under
/// <c>SnapshotAssertions.Render</c>.
/// </para>
/// <para>
/// This type is intentionally internal: it exists purely to give the namespace a stable
/// identity at the assembly level. No public surface ships under
/// <c>SnapshotAssertions.Render</c> from this package today; Snap-internal renderers
/// (if any are added) live here.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2094:Classes should not be empty", Justification = "Intentional namespace anchor. C# requires at least one type to make a namespace exist at the assembly level; this internal empty type reserves SnapshotAssertions.Render for sibling-package text renderers per the family convention documented in CONVENTIONS.md.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "Intentional namespace anchor; it has no callers by design (see SuppressMessage above and CONVENTIONS.md).")]
internal static class NamespaceAnchor
{
}
