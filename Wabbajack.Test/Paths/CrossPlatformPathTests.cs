namespace Wabbajack.Paths.Test;

/// <summary>
/// Characterization tests documenting CURRENT (Windows-biased) path behavior ahead of the planned
/// Linux support. These pin today's semantics — including the known quirks — so the Linux work can
/// flip them from "documents the quirk" to "asserts the fix". See AbsolutePath / RelativePath.
/// </summary>
public class CrossPlatformPathTests
{
    [Test]
    public async Task AbsolutePath_ParsesUnixStylePaths()
    {
        var p = (AbsolutePath) "/foo/bar/baz.txt";

        await Assert.That(p.FileName.ToString()).IsEqualTo("baz.txt");
        await Assert.That(p.Parent).IsEqualTo((AbsolutePath) "/foo/bar");
        await Assert.That(p.Extension).IsEqualTo(new Extension(".txt"));
    }

    [Test]
    public async Task AbsolutePath_UnixPathRoundTripsWithForwardSlashes()
    {
        await Assert.That(((AbsolutePath) "/foo/bar").ToString()).IsEqualTo("/foo/bar");
    }

    [Test]
    public async Task RelativePath_AcceptsForwardSlashesButEmitsBackslashes()
    {
        // KNOWN QUIRK: RelativePath does not track path format and always renders with '\', even
        // when built from a unix-style string. Phase 5 (Linux) should make this round-trip per-OS.
        await Assert.That(((RelativePath) "foo/bar/baz.txt").ToString()).IsEqualTo(@"foo\bar\baz.txt");
    }

    [Test]
    public async Task Paths_AreCaseInsensitive_CurrentBehavior()
    {
        // KNOWN: comparisons are case-insensitive everywhere (correct for Windows, wrong for Linux,
        // where file names are case-sensitive). Phase 5 must choose a platform-aware policy.
        await Assert.That(((AbsolutePath) @"c:\Foo").Equals((AbsolutePath) @"c:\foo")).IsTrue();
        await Assert.That(((RelativePath) "Foo/Bar").Equals((RelativePath) "foo/bar")).IsTrue();
    }
}
