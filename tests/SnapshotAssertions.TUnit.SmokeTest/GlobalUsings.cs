// Mirrors the GlobalUsings.cs recommendation in SnapshotAssertions.TUnit's README. The
// smoke-test project deliberately uses <ImplicitUsings>disable</ImplicitUsings> so a failure
// to wire up these usings: or a future change that breaks the auto-discovery of
// SnapshotAssertions.TUnit's [AssertionExtension]-emitted entry points: surfaces as a build
// failure here rather than silently passing in our own test project (which lives in the
// SnapshotAssertions.TUnit.Tests namespace and gets parent-namespace visibility for free).

global using System;                            // String, etc.
global using System.IO;                         // Path, File
global using System.Threading;                  // CancellationToken
global using System.Threading.Tasks;            // Task
global using SnapshotAssertions;                // SnapshotOptions and friends
