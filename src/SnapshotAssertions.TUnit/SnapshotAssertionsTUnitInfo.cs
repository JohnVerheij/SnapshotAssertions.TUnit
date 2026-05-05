namespace SnapshotAssertions.TUnit;

/// <summary>
/// Static placeholder type that gives the SnapshotAssertions.TUnit assembly a public surface
/// during initial scaffold. Replaced by the real public API (the <c>MatchesSnapshot</c> /
/// <c>MatchesSnapshotFile</c> assertion entry points wired via TUnit's
/// <c>[AssertionExtension]</c> source generator) in the 0.1.0 implementation pass.
/// </summary>
/// <remarks>
/// This type intentionally has a single read-only constant. Once the real assertion entry
/// points ship, this type is removed from the public surface; the package's only public
/// API will be the auto-generated <c>Assert.That(...).MatchesSnapshot(...)</c> chain.
/// </remarks>
public static class SnapshotAssertionsTUnitInfo
{
    /// <summary>Marker constant identifying this assembly as a pre-release scaffold. Not
    /// part of the stable surface; will be removed when the 0.1.0 public API ships.</summary>
    public const string ScaffoldMarker = "snapshot-assertions-tunit-0.1.0-scaffold";
}
